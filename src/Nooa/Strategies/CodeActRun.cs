// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (CodeActStrategy._run_generation and the tool handlers)

using System.Text.Json.Nodes;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Llm;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// One CodeAct generation: the prefill, then the loop of model turns. A turn is one model call and the tool calls in its reply.
/// The run owns the session counters, the loop guard and the engine of the call, so one strategy instance can serve many calls.
/// </summary>
/// <remarks>
/// <para>
/// <b>Untrusted input.</b> The model chooses the tool name, the arguments and the code. A tool name that goes into a message is
/// bounded. No exception message holds model text: the feedback that carries a model value goes to events, which the renderer escapes.
/// </para>
/// <para>
/// <b>Abandoned cell.</b> A cell that ignores its time limit poisons the engine (see <see cref="IExecutionEngine.IsAbandoned"/>).
/// The run records the output of that cell and ends the call with a <see cref="GenerationException"/>.
/// </para>
/// </remarks>
internal sealed class CodeActRun(
    string strategyName,
    CodeActConfig config,
    IRuntimeServices runtime,
    CurrentCall call,
    IExecutionEngine engine)
{
    private readonly CodeActSession _session = new(config.MaxIterations, config.MaxRetries, call.MethodName);
    private readonly LoopGuard? _guard = config.LoopGuard is { } guard ? new LoopGuard(guard) : null;
    private readonly FormatConfig _format = runtime.TruncationConfig.EventFormat;
    private readonly ReturnResultValidator _validator = new(runtime.Agent, call, engine, config, runtime.TruncationConfig.EventFormat);
    private readonly IReadOnlyList<ToolDefinition> _tools = [CodeActPrompts.CodeTool(), CodeActPrompts.ReturnTool(call.ReturnType)];

    private EventManager Events => runtime.EventManager;

    public async Task<object?> RunAsync(CancellationToken cancellationToken)
    {
        Events.Add(new TaskEvent { Prompt = CodeActPrompts.TaskMessage(call) });

        // Method preconditions fail fast: an exception ends the call.
        StrategyConditions.RunPreconditions(runtime.Agent, call, config.Preconditions);

        await RunPrefillAsync(cancellationToken).ConfigureAwait(false);

        var generationId = runtime.GenerationId
            ?? throw new NooaRuntimeException("CodeAct needs a generation session. The runtime did not set a generation id.");
        var parentGenerationId = runtime.ParentGenerationId;
        var turnNumber = 0;
        while (!_session.IsExhausted)
        {
            turnNumber++;
            var state = new TurnState();
            Events.Add(
                new BeforeTurn { MethodName = call.MethodName, Strategy = strategyName, GenerationId = generationId, ParentGenerationId = parentGenerationId, TurnNumber = turnNumber },
                record: false);
            try
            {
                if (await TurnAsync(cancellationToken).ConfigureAwait(false) is { } completion)
                {
                    state.Success = true;
                    state.IsFinal = true;
                    return completion.Value;
                }
            }
            catch (Exception e)
            {
                state.ExceptionType = e.GetType().Name;
                state.IsFinal = true;
                throw;
            }
            finally
            {
                Events.Add(
                    new AfterTurn
                    {
                        MethodName = call.MethodName,
                        Strategy = strategyName,
                        GenerationId = generationId,
                        ParentGenerationId = parentGenerationId,
                        TurnNumber = turnNumber,
                        IsFinal = state.IsFinal,
                        Success = state.IsFinal ? state.Success : null,
                        ExceptionType = state.ExceptionType,
                    },
                    record: false);
            }
        }

        // The loop ended without a result.
        Events.Add(
            new AfterTurn
            {
                MethodName = call.MethodName,
                Strategy = strategyName,
                GenerationId = generationId,
                ParentGenerationId = parentGenerationId,
                TurnNumber = turnNumber,
                IsFinal = true,
                Success = false,
                ExceptionType = nameof(GenerationException),
            },
            record: false);
        throw _session.BuildFailure();
    }

    private static string FinishName(FinishReason reason) => reason switch
    {
        FinishReason.ToolCalls => "tool_calls",
        FinishReason.Length => "length",
        FinishReason.Error => "error",
        _ => "stop",
    };

    private static string ExceptionChain(Exception error)
    {
        var parts = new List<string>();
        for (Exception? e = error; e is not null && parts.Count < 5; e = e.InnerException)
        {
            parts.Add($"{e.GetType().Name}: {e.Message}");
        }

        return string.Join(" <- ", parts);
    }

    private static string LastLine(string text) =>
        text.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "";

    private static JsonNode? TryParse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>The tool name as it goes into an exception message: only the names of the two tools, never model text.</summary>
    private static string SafeToolName(string name) =>
        name is CodeActPrompts.CodeToolName or CodeActPrompts.ReturnToolName ? name : "unknown tool";

    private async Task<Completion?> TurnAsync(CancellationToken cancellationToken)
    {
        GenerateResult generated;
        try
        {
            // The conversation is rebuilt from the events on every call, so the events of earlier turns change what the model sees.
            var messages = runtime.BuildMessages(config.MaxTokens);
            generated = await runtime.GenerateAsync(messages, _tools, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            // The LLM layer already retries rate limits and transient failures. This is what is left after that.
            _session.RecordError();
            Events.Add(new Error { Content = $"LLM API error (attempt {_session.ErrorCount}/{_session.MaxRetries}): {ExceptionChain(e)}" });
            if (_session.IsExhausted)
            {
                throw new GenerationException(
                    $"LLM API error after {_session.MaxRetries} retries. Original error: {e.GetType().Name}: {e.Message}", e);
            }

            return null;
        }

        var response = generated.Response;
        if (response.FinishReason == FinishReason.Length)
        {
            _session.RecordError();
            Events.Add(new DebugTrace { Content = $"Truncated response: {DebugDetails(response)}" });
            throw new GenerationException(CodeActPrompts.OutputTokensExhausted);
        }

        if (response.FinishReason == FinishReason.Error)
        {
            _session.RecordError();
            Events.Add(new DebugTrace { Content = $"Failed response: {DebugDetails(response)}" });
            throw new GenerationException("The model returned an incomplete response.");
        }

        if (response.FinishReason == FinishReason.ToolCalls && response.ToolCalls.Count > 0)
        {
            // A real tool call counts as progress: it resets the guard against a model that drifts into prose.
            _session.ResetTextOnly();
            return await ProcessToolCallsAsync(response.ToolCalls, response.Id, cancellationToken).ConfigureAwait(false);
        }

        var text = response.Content;
        var hasText = text.Trim().Length > 0;
        if (hasText || response.FinishReason == FinishReason.Stop)
        {
            // The reply stays in the history as the assistant turn. The correction goes after it.
            Events.Add(new TextOnlyReply
            {
                Content = text,
                FinishReason = FinishName(response.FinishReason),
                Handler = "retry_text_only_response",
                Action = "retry",
                ConsecutiveTextOnly = _session.ConsecutiveTextOnly + 1,
            });
            _session.RecordIteration();
            Events.Add(new Error { Content = CodeActPrompts.TextOnlyCorrection(call) });
            _session.RecordTextOnly();
            var limit = config.MaxConsecutiveTextOnly;
            if (limit > 0 && _session.ConsecutiveTextOnly >= limit)
            {
                throw new GenerationException(
                    $"CodeAct aborted: the model returned plain text without a tool call {_session.ConsecutiveTextOnly} times in a row "
                    + $"(MaxConsecutiveTextOnly={limit}) for `{call.MethodName}`. The agent likely thinks it is done: "
                    + "it must call `return_result(...)` to finish.");
            }

            return null;
        }

        _session.RecordError();
        Events.Add(new DebugTrace { Content = $"Empty response: {DebugDetails(response)}" });
        Events.Add(new Error { Content = CodeActPrompts.ToolUseReminder("Empty response received.") });
        return null;
    }

    private string DebugDetails(LlmResponse response) =>
        $"finish_reason={FinishName(response.FinishReason)}; content={PFormat.Format(response.Content, _format)}; tool_calls={response.ToolCalls.Count}";

    private async Task<Completion?> ProcessToolCallsAsync(IReadOnlyList<ToolCall> toolCalls, string responseId, CancellationToken cancellationToken)
    {
        // A model may send several tool calls in one reply. They run in order and stop at the first error,
        // because a later cell probably depends on the output of the failed one.
        _session.RecordIteration();
        foreach (var toolCall in toolCalls)
        {
            ToolStep step;
            if (_guard is null)
            {
                step = await ProcessOneAsync(toolCall, responseId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var parsed = TryParse(toolCall.Arguments);
                var action = LoopGuard.Fingerprint(toolCall.Name, parsed, toolCall.Arguments, CodeActPrompts.CodeToolName);
                if (GuardBefore(_guard, action, parsed, toolCall, responseId) is { } blocked)
                {
                    return blocked.Completion;
                }

                step = await ProcessOneAsync(toolCall, responseId, cancellationToken).ConfigureAwait(false);
                if (step.Kind != StepKind.Completed)
                {
                    GuardAfter(_guard, action, parsed, toolCall, step);
                }
            }

            if (step.Kind == StepKind.Completed)
            {
                return step.Completion;
            }

            if (step.Kind == StepKind.StopTurn)
            {
                return null;
            }
        }

        return null;
    }

    private ToolStep? GuardBefore(LoopGuard guard, string action, JsonNode? parsed, ToolCall toolCall, string responseId)
    {
        if (guard.Before(action) is not { } verdict)
        {
            return null;
        }

        var message = guard.Message(verdict, SafeToolName(toolCall.Name), LoopGuard.MentionsReturnResult(parsed));
        Events.Add(new ToolCallEvent
        {
            ToolCallId = toolCall.Id,
            Name = toolCall.Name,
            Arguments = parsed as JsonObject ?? [],
            LlmResponseId = responseId,
            Result = new ToolResult { ToolCallId = toolCall.Id, Content = "Not executed: blocked by the loop guard.", ResultStatus = ResultStatus.Error },
        });
        RecordLoopGuard(message, verdict.Action == LoopGuardAction.Block ? "blocked" : "stopped", toolCall, verdict.Repeats, action);
        return verdict.Action == LoopGuardAction.Stop ? throw new LoopDetectedException(message) : ToolStep.Stop(failed: true, message);
    }

    private void GuardAfter(LoopGuard guard, string action, JsonNode? parsed, ToolCall toolCall, ToolStep step)
    {
        if (guard.After(action, step.Outcome, step.Failed) is not { } verdict)
        {
            return;
        }

        var message = guard.Message(verdict, SafeToolName(toolCall.Name), LoopGuard.MentionsReturnResult(parsed));
        RecordLoopGuard(message, verdict.Action == LoopGuardAction.Nudge ? "nudged" : "stopped", toolCall, verdict.Repeats, action);
        if (verdict.Action == LoopGuardAction.Stop)
        {
            throw new LoopDetectedException(message);
        }
    }

    private void RecordLoopGuard(string message, string kind, ToolCall toolCall, int repeats, string action) =>
        Events.Add(new LoopGuardTriggered
        {
            Content = message,
            Action = kind,
            ToolName = CodeActPrompts.Bound(toolCall.Name),
            ToolCallId = toolCall.Id,
            Repeats = repeats,
            Window = _guard!.Config.Window,
            Fingerprint = action,
        });

    private async Task<ToolStep> ProcessOneAsync(ToolCall toolCall, string responseId, CancellationToken cancellationToken)
    {
        if (TryParse(toolCall.Arguments) is not JsonObject args)
        {
            _session.RecordError();
            var content =
                $"Invalid arguments for tool `{CodeActPrompts.Bound(toolCall.Name)}`: the arguments are not a JSON object. "
                + $"Tool arguments must be a JSON object; call {CodeActPrompts.CodeToolName} with arguments like {{\"code\": \"Print(1);\"}}.";
            Events.Add(new Error { Content = content });
            return ToolStep.Stop(failed: true, content);
        }

        var tag = Events.Add(new ToolCallEvent
        {
            ToolCallId = toolCall.Id,
            Name = toolCall.Name,
            Arguments = (JsonObject)args.DeepClone(),
            LlmResponseId = responseId,
        });
        try
        {
            return toolCall.Name switch
            {
                CodeActPrompts.CodeToolName => await HandleCodeAsync(toolCall, args, tag, cancellationToken).ConfigureAwait(false),
                CodeActPrompts.ReturnToolName => HandleReturnResult(toolCall, args, tag),
                _ => HandleUnknownTool(toolCall, args, tag),
            };
        }
        catch (Exception e)
        {
            // A tool call with no result corrupts the next render. Close it before the exception leaves.
            Events.Update(tag, ev =>
            {
                if (ev is ToolCallEvent { Result: null } open)
                {
                    open.Result = new ToolResult { ToolCallId = toolCall.Id, Content = $"Tool call failed: {e.GetType().Name}.", ResultStatus = ResultStatus.Error };
                }
            });
            throw;
        }
    }

    private ToolStep HandleUnknownTool(ToolCall toolCall, JsonObject args, string tag)
    {
        _session.RecordError();
        var content = BlockWrapper.EscapeClosingTags(CodeActPrompts.UnknownToolFeedback(toolCall.Name, args, runtime.Agent.GetType()));
        SetResult(tag, toolCall.Id, content, ResultStatus.Error);
        return ToolStep.Stop(failed: true, content);
    }

    private ToolStep HandleReturnResult(ToolCall toolCall, JsonObject args, string tag)
    {
        var check = _validator.CheckToolArguments(args);
        if (check.IsValid)
        {
            var corrected = ReturnResultValidator.CorrectedArguments(check.Value, args);
            Events.Update(tag, ev =>
            {
                if (ev is ToolCallEvent open && !ReferenceEquals(corrected, args))
                {
                    open.Arguments.Clear();
                    foreach (var (key, value) in corrected)
                    {
                        open.Arguments[key] = value?.DeepClone();
                    }
                }
            });
            SetResult(tag, toolCall.Id, "Result accepted.", ResultStatus.Complete);
            return ToolStep.Complete(check.Value);
        }

        _session.RecordError();
        if (_session.IsExhausted)
        {
            SetResult(tag, toolCall.Id, "return_result validation failed (session exhausted).", ResultStatus.Error);
            Events.Add(new Error { Content = check.Error! });
            throw ValidationExhausted();
        }

        // A tool result is not wrapped by the block formatter, so closing tags in the echoed model value are neutralized here.
        var content = $"Invalid result: {BlockWrapper.EscapeClosingTags(check.Error!)}\nPlease call return_result again with valid arguments. "
            + $"Tip: if you computed the result in {CodeActPrompts.CodeToolName}(), you can call ReturnResult(variable) from within the code instead.";
        SetResult(tag, toolCall.Id, content, ResultStatus.Error);
        return ToolStep.Stop(failed: true, content);
    }

    private async Task<ToolStep> HandleCodeAsync(ToolCall toolCall, JsonObject args, string tag, CancellationToken cancellationToken)
    {
        var raw = args["code"] is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";
        var code = ResponseCleanup.StripCodeFences(raw).Code;
        var empty = string.IsNullOrWhiteSpace(code);

        // An empty cell still takes a cell number, as in the engine. That keeps the numbers of In[n] and Out[n] unique.
        var result = await RunCellAsync(tag, toolCall.Id, code, cancellationToken).ConfigureAwait(false);
        var failed = empty || result.Status != ResultStatus.Complete;
        var error = empty ? CodeActPrompts.EmptyCodeError(args) : result.Error;
        var status = empty ? ResultStatus.Error : result.Status;
        var stderr = result.Stderr;
        SetResultStatus(tag, toolCall.Id, status);
        if (empty)
        {
            _session.RecordError();
        }

        // ReturnResult(x) in a cell, and a top-level return x;, end the task with the value.
        ReturnCheck? check = null;
        if (!failed && result.ExplicitReturn)
        {
            check = _validator.Check(result.Value);
            if (!check.IsValid)
            {
                _session.RecordError();
                stderr = stderr.Length > 0
                    ? $"{stderr}\nreturn_result validation error: {check.Error}"
                    : $"return_result validation error: {check.Error}";
                status = ResultStatus.Error;
                SetResultStatus(tag, toolCall.Id, ResultStatus.Error);
            }
        }

        var returned = check is { IsValid: true };
        Events.Add(new CellOutput
        {
            ToolCallId = toolCall.Id,
            ExecutionCount = result.ExecutionCount,
            Stdout = result.Stdout,
            Stderr = stderr,
            Error = error,
            Value = failed || check is { IsValid: false } ? null : CellValues.ToJson(returned ? check!.Value : result.Value, _format),
            ExplicitReturn = !failed && result.ExplicitReturn,
            ExecutionStatus = status,
        });
        ThrowIfStopped(result, cancellationToken);

        if (check is { IsValid: false } && _session.IsExhausted)
        {
            Events.Add(new Error { Content = check.Error! });
            throw ValidationExhausted();
        }

        if (returned)
        {
            EmitInlineReturn(check!.Value);
            return ToolStep.Complete(check.Value);
        }

        var failure = failed || check is { IsValid: false };
        var outcome = failure
            ? LastLine(check?.Error is { } feedback ? $"return_result validation error: {feedback}" : error)
            : string.Join('\n', new[] { result.Stdout, result.Stderr, ShowValue(result.Value) }.Where(p => p.Length > 0));
        return failure ? ToolStep.Stop(failed: true, outcome) : ToolStep.Continue(outcome);
    }

    private string ShowValue(object? value) => value is null ? "null" : PFormat.Format(value, _format);

    private async Task<CellResult> RunCellAsync(string tag, string toolCallId, string code, CancellationToken cancellationToken)
    {
        // The receipt text must not change after a generation has seen it: a nested call may render it before the cell ends.
        SetResult(tag, toolCallId, CodeActPrompts.CellReceipt, ResultStatus.Running);
        var agent = runtime.Agent;
        var hook = InstrumentationHooks.CallBefore(
            agent.Logger, h => h.BeforeCodeExecution(agent, call.MethodName, code, toolCallId, runtime.GenerationId));
        CellResult result;
        try
        {
            result = await engine.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            var failed = new CellResult(engine.ExecutionCount, ResultStatus.Error, Error: e.GetType().Name);
            InstrumentationHooks.CallAfter(agent.Logger, h => h.AfterCodeExecution(hook, agent, call.MethodName, failed));
            throw;
        }

        InstrumentationHooks.CallAfter(agent.Logger, h => h.AfterCodeExecution(hook, agent, call.MethodName, result));
        return result;
    }

    /// <summary>Ends the call when the cell was cancelled or abandoned. The output of the cell is already recorded.</summary>
    private void ThrowIfStopped(CellResult result, CancellationToken cancellationToken)
    {
        if (engine.IsAbandoned)
        {
            throw new GenerationException(
                "A cell did not stop after its time limit, so the session cannot run more cells. "
                + "Check for infinite loops, and pass the Cancellation token to long calls.");
        }

        if (result.Status == ResultStatus.Cancelled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private GenerationException ValidationExhausted() =>
        new($"return_result validation failed after {_session.MaxRetries} attempts for `{call.MethodName}`. The last feedback is in the Error event.");

    private void SetResult(string tag, string toolCallId, string content, ResultStatus status) =>
        Events.Update(tag, ev =>
        {
            if (ev is ToolCallEvent open)
            {
                open.Result = new ToolResult { ToolCallId = toolCallId, Content = content, ResultStatus = status };
            }
        });

    /// <summary>Changes the lifecycle status of a cell receipt. The text stays.</summary>
    private void SetResultStatus(string tag, string toolCallId, ResultStatus status) =>
        SetResult(tag, toolCallId, CodeActPrompts.CellReceipt, status);

    private void EmitInlineReturn(object? value)
    {
        // The final answer would be invisible in the event stream otherwise. The marker is observability data only.
        var id = "codeact_inline_" + Guid.NewGuid().ToString("N")[..8];
        Events.Add(new ToolCallEvent
        {
            ToolCallId = id,
            Name = CodeActPrompts.ReturnToolName,
            Arguments = new JsonObject { ["result"] = CellValues.ToJson(value, _format) },
            Result = new ToolResult { ToolCallId = id, Content = "Result accepted (inline).", ResultStatus = ResultStatus.Complete },
            Metadata = new JsonObject { ["synthetic"] = true, ["synthetic_type"] = "codeact_inline_return" },
        });
    }

    private async Task RunPrefillAsync(CancellationToken cancellationToken)
    {
        if (config.Prefill?.GetCode(call, runtime.TruncationConfig) is not { } code || string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        try
        {
            var id = "prefill_" + Guid.NewGuid().ToString("N")[..8];
            var tag = Events.Add(new ToolCallEvent
            {
                ToolCallId = id,
                Name = CodeActPrompts.CodeToolName,
                Arguments = new JsonObject { ["code"] = code },
                Metadata = PrefillMetadata(failed: false),
            });
            var result = await RunCellAsync(tag, id, code, cancellationToken).ConfigureAwait(false);
            var failed = result.Status != ResultStatus.Complete;
            SetResultStatus(tag, id, result.Status);
            Events.Add(new CellOutput
            {
                ToolCallId = id,
                ExecutionCount = result.ExecutionCount,
                Stdout = result.Stdout,
                Stderr = result.Stderr,
                Error = result.Error,
                Value = failed ? null : CellValues.ToJson(result.Value, _format),
                ExecutionStatus = result.Status,
                Metadata = PrefillMetadata(failed),
            });
            ThrowIfStopped(result, cancellationToken);
        }
        catch (Exception e) when (e is not (OperationCanceledException or GenerationException))
        {
            // The prefill only helps the model. A failure must not end the call.
            Events.Add(new Error { Content = $"Prefill error: {e.Message}" });
        }
    }

    private static JsonObject PrefillMetadata(bool failed)
    {
        var metadata = new JsonObject { ["prefill"] = true, ["prefill_type"] = "inspect_inputs" };
        if (failed)
        {
            metadata["execution_error"] = true;
        }

        return metadata;
    }

    private sealed record Completion(object? Value);

    private enum StepKind
    {
        Continue,
        StopTurn,
        Completed,
    }

    private sealed record ToolStep(StepKind Kind, bool Failed, string Outcome, Completion? Completion = null)
    {
        public static ToolStep Continue(string outcome) => new(StepKind.Continue, false, outcome);

        public static ToolStep Stop(bool failed, string outcome) => new(StepKind.StopTurn, failed, outcome);

        public static ToolStep Complete(object? value) => new(StepKind.Completed, false, "", new Completion(value));
    }

    private sealed class TurnState
    {
        public bool Success { get; set; }

        public bool IsFinal { get; set; }

        public string? ExceptionType { get; set; }
    }
}
