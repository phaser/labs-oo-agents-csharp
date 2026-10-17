// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Json;
using Nooa.Llm;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// The runtime of one agent: it serializes generation, resolves the strategy and the LLM of a call, and runs the
/// strategy. It is trimmed to what the Template, Predict and CodeAct strategies need.
/// </summary>
/// <remarks>
/// One generation session runs at a time per agent (decision D10): a <see cref="SemaphoreSlim"/> guards it, and an
/// ambient flag lets a nested call from inside a session run inline without the lock. A strategy with
/// <see cref="GenerationStrategy.RequiresLock"/> false skips the lock.
/// </remarks>
public sealed class ActorRuntime : IRuntimeServices
{
    private readonly SemaphoreSlim _generationLock = new(1, 1);
    private readonly AgentMethodInvoker _invoker;
    private readonly ConcurrentDictionary<string, int> _turns = new();
    private int? _lastPromptTokensActual;
    private ContextWindowStats? _lastContextStats;

    /// <summary>The characters-to-tokens ratio before a response calibrates it: about 4 characters per token.</summary>
    private const double _defaultTokensPerChar = 0.25;

    private double _tokensPerChar = _defaultTokensPerChar;

    /// <summary>Creates the runtime of an agent.</summary>
    public ActorRuntime(Agent agent)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _invoker = new AgentMethodInvoker(this);
    }

    /// <inheritdoc />
    public Agent Agent { get; }

    /// <inheritdoc />
    public EventManager EventManager => Agent.EventManager;

    /// <inheritdoc />
    public TruncationConfig TruncationConfig => AgentContext.CurrentTruncation ?? Agent.Truncation;

    /// <inheritdoc />
    public CurrentCall? CurrentCall => AgentContext.CurrentCall;

    /// <inheritdoc />
    public string? GenerationId => AgentContext.GenerationIds is { Count: > 0 } ids ? ids[^1] : null;

    /// <inheritdoc />
    public string? ParentGenerationId => AgentContext.GenerationIds is { Count: > 1 } ids ? ids[^2] : null;

    /// <summary>The id of the innermost agent call, or null outside a call.</summary>
    public string? AgentCallId => AgentContext.CurrentCallId;

    /// <summary>The provider-reported prompt token count of the latest generation, or null before the first response.</summary>
    public int? LastPromptTokensActual => _lastPromptTokensActual;

    /// <inheritdoc />
    public ContextWindowStats? LastContextStats => _lastContextStats;

    /// <inheritdoc />
    /// <remarks>
    /// The context budget is <see cref="TruncationConfig.MaxContextTokens"/>. When it is not set and the model window is
    /// known, the budget is half of the usable window (the window minus the output reserve). Token sizes are
    /// characters times a ratio that the prompt tokens of each response calibrate.
    /// </remarks>
    public IReadOnlyList<LlmMessage> BuildMessages(int? maxOutputTokens = null)
    {
        var client = AgentContext.CurrentLlm
            ?? throw new NooaRuntimeException("BuildMessages was called with no LLM client in context.");
        var truncation = TruncationConfig;
        var limits = ContextLimits.For(client, maxOutputTokens, truncation.ResponseReserveTokens);
        var build = ContextBuilder.Build(
            Agent.ContextManager,
            EventManager,
            AgentContext.CurrentStrategy,
            truncation.ContextBlockFormat,
            ScopedContext.CurrentBlocks,
            EventManager.GetEventQuery(),
            ScopedContext.CurrentEvents,
            Agent.EventQuery,
            AgentCallId,
            Agent.Logger,
            Agent.ContextManager.Expressions);
        Agent.ContextManager.UpdateResolved(build.ResolvedCache);

        var budget = truncation.MaxContextTokens;
        if (budget is null && limits.ContextWindow is not null && truncation.ResponseReserveTokens > 0)
        {
            budget = Math.Max(0, limits.ContextWindow.Value - limits.ReservedOutputTokens) / 2;
        }

        var ratio = _tokensPerChar;
        var result = ContextRenderer.Render(
            build.Blocks,
            Agent.BlockFormatter,
            contextLimit: budget,
            countTokens: text => (int)Math.Round(text.Length * ratio, MidpointRounding.ToEven),
            eventFormat: truncation.EventFormat,
            eventFormatResolver: EventFormatOf,
            modelContextWindow: limits.ContextWindow,
            reservedOutputTokens: limits.ReservedOutputTokens,
            logger: Agent.Logger);
        _lastContextStats = result.Stats with { OutputReserveIsFallback = limits.ReserveIsFallback };
        _lastPromptTokensActual = null;
        return result.Messages;
    }

    /// <summary>Runs one agentic call through the method wrapper. <see cref="Agent"/> calls this.</summary>
    internal Task<object?> InvokeAsync(CurrentCall call, CancellationToken cancellationToken) =>
        AgentContext.EvaluatingExpression
            ? throw new NooaRuntimeException(
                $"{call.MethodName} cannot run while a context expression is evaluated. An expression must not call an LLM method.")
            : _invoker.InvokeAsync(call, cancellationToken);

    /// <summary>Takes the generation lock when needed and runs the strategy of the call.</summary>
    internal async Task<object?> DispatchAsync(CurrentCall call, MethodInfo? method, CancellationToken cancellationToken)
    {
        var strategy = Agent.ResolveStrategy(call);
        if (AgentContext.InGenerationSession || !strategy.RequiresLock)
        {
            return await ExecuteWithGenerationAsync(strategy, call, method, cancellationToken).ConfigureAwait(false);
        }

        await _generationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ExecuteWithGenerationAsync(strategy, call, method, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _generationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<GenerateResult> GenerateAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var call = AgentContext.CurrentCall
            ?? throw new NooaRuntimeException("GenerateAsync was called with no current call context.");
        var client = AgentContext.CurrentLlm
            ?? throw new NooaRuntimeException("GenerateAsync was called with no LLM client in context.");
        var generationId = GenerationId ?? "";
        var turn = _turns.AddOrUpdate(generationId, 1, (_, n) => n + 1);
        var strategyName = AgentContext.CurrentStrategy?.Name ?? "default";
        EventManager.Add(new LlmCallStart { MethodName = call.MethodName, Strategy = strategyName, GenerationId = generationId, TurnNumber = turn }, record: false);

        LlmResponse response;
        try
        {
            if (EventManager.HasMiddleware<LlmCallContext>())
            {
                var context = new LlmCallContext
                {
                    Messages = messages,
                    Tools = tools,
                    OutputType = outputType,
                    Agent = Agent,
                    Runtime = this,
                    Client = client,
                };
                context = await EventManager.RunMiddlewareAsync(context, async ctx =>
                {
                    ctx.Response = await ctx.Client.CallAsync(ctx.Messages, ctx.Tools, ctx.OutputType, cancellationToken).ConfigureAwait(false);
                    return ctx;
                }).ConfigureAwait(false);
                response = context.Response
                    ?? throw new NooaRuntimeException(
                        "LLM call middleware returned without setting the response. A short-circuiting middleware must set the response before it returns.");
            }
            else
            {
                response = await client.CallAsync(messages, tools, outputType, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            EventManager.Add(new LlmCallEnd { MethodName = call.MethodName, Strategy = strategyName, GenerationId = generationId, TurnNumber = turn, Success = false, ExceptionType = ex.GetType().Name }, record: false);
            throw;
        }

        EventManager.Add(new LlmCallEnd { MethodName = call.MethodName, Strategy = strategyName, GenerationId = generationId, TurnNumber = turn }, record: false);
        if (response.Tag is not null)
        {
            throw new NooaRuntimeException(
                "LLM middleware returned an already recorded response. Each call must return a fresh response object.");
        }

        response = response with { GenerationId = generationId };
        if (response.Usage is { InputTokens: > 0 } usage)
        {
            _lastPromptTokensActual = usage.InputTokens;
            if (_lastContextStats is { } stats)
            {
                _lastContextStats = stats with { PromptTokens = usage.InputTokens };
                var totalChars = stats.ContextBlocksChars + stats.EventsChars;
                if (totalChars > 0)
                {
                    _tokensPerChar = (double)usage.InputTokens / totalChars;
                }
            }
        }

        return new GenerateResult(response, EventManager.Add(response));
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteNestedAsync(GenerationStrategy strategy, CurrentCall call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(call);
        var generationId = Guid.NewGuid().ToString();
        var parentGenerationId = GenerationId;
        AgentContext.PushGeneration(generationId);
        var hookContext = BeforeGeneration(strategy, call.MethodName, generationId, parentGenerationId);
        object? result = null;
        Exception? failure = null;
        try
        {
            result = await strategy.ExecuteAsync(this, call, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            AgentContext.PopGeneration();
            AfterGeneration(strategy, hookContext, call.MethodName, result, failure, generationId);
        }
    }

    private async Task<object?> ExecuteWithGenerationAsync(
        GenerationStrategy strategy, CurrentCall call, MethodInfo? method, CancellationToken cancellationToken)
    {
        var llm = ResolveLlm(call, method);
        var truncation = Agent.Truncation;
        var previousSession = AgentContext.InGenerationSession;
        var previousCall = AgentContext.CurrentCall;
        var previousLlm = AgentContext.CurrentLlm;
        var previousStrategy = AgentContext.CurrentStrategy;
        var previousTruncation = AgentContext.CurrentTruncation;
        var previousFormat = AgentContext.CurrentEventFormat;
        var generationId = Guid.NewGuid().ToString();
        var parentGenerationId = GenerationId;

        AgentContext.InGenerationSession = true;
        AgentContext.PushGeneration(generationId);
        AgentContext.CurrentCall = call;
        AgentContext.CurrentLlm = llm;
        AgentContext.CurrentStrategy = strategy;
        AgentContext.CurrentTruncation = truncation;
        AgentContext.CurrentEventFormat = JsonSerializer.SerializeToNode(truncation.EventFormat, NooaJson.Options);
        var hookContext = BeforeGeneration(strategy, call.MethodName, generationId, parentGenerationId);
        object? result = null;
        Exception? failure = null;
        try
        {
            result = await strategy.ExecuteAsync(this, call, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            AgentContext.PopGeneration();
            _turns.TryRemove(generationId, out _);
            AgentContext.CurrentCall = previousCall;
            AgentContext.CurrentLlm = previousLlm;
            AgentContext.CurrentStrategy = previousStrategy;
            AgentContext.CurrentTruncation = previousTruncation;
            AgentContext.CurrentEventFormat = previousFormat;
            AgentContext.InGenerationSession = previousSession;
            AfterGeneration(strategy, hookContext, call.MethodName, result, failure, generationId);
        }
    }

    /// <summary>
    /// The format bounds for one event. An event that was added during a generation carries the bounds of that
    /// generation in its metadata, so history stays the same after a session resume. Other events use the agent bounds.
    /// Stored metadata is not trusted: bounds that do not parse or do not validate are ignored.
    /// </summary>
    private FormatConfig EventFormatOf(EventBase evt)
    {
        if (evt.Metadata["truncation_event_format"] is { } node)
        {
            try
            {
                var format = node.Deserialize<FormatConfig>(NooaJson.Options);
                format?.Validate();
                if (format is not null)
                {
                    return format;
                }
            }
            catch (Exception ex) when (ex is JsonException or ConfigValidationException)
            {
                Agent.Logger.LogWarning(ex, "Event {EventId} has invalid stored format bounds. The agent bounds apply.", evt.Id);
            }
        }

        return Agent.Truncation.EventFormat;
    }

    /// <summary>The LLM cascade of a call: call override, then method, then the agent client (instance, class, parent).</summary>
    private ILlmClient ResolveLlm(CurrentCall call, MethodInfo? method)
    {
        if (AgentContext.LlmOverride is { } scoped && ReferenceEquals(scoped.Agent, Agent))
        {
            return scoped.Client;
        }

        if (method?.GetCustomAttribute<AgentOptionsAttribute>()?.Llm is { Length: > 0 } alias)
        {
            return Agent.ResolveAlias(alias, $"[AgentOptions] on {call.MethodName}");
        }

        return Agent.Llm;
    }

    private object? BeforeGeneration(GenerationStrategy strategy, string methodName, string generationId, string? parentGenerationId)
    {
        if (!strategy.Traceable)
        {
            return null;
        }

        return InstrumentationHooks.CallBefore(
            Agent.Logger,
            h => h.BeforeGeneration(Agent, methodName, strategy.Name, generationId, parentGenerationId, AgentCallId));
    }

    private void AfterGeneration(
        GenerationStrategy strategy, object? hookContext, string methodName, object? result, Exception? failure, string generationId)
    {
        if (strategy.Traceable)
        {
            InstrumentationHooks.CallAfter(
                Agent.Logger, h => h.AfterGeneration(hookContext, Agent, methodName, result, failure, generationId));
        }
    }
}
