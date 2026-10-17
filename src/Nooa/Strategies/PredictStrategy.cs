// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/predict.py

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Json;
using Nooa.Llm;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// A single-shot model call that tries to solve the task in one go, with no loop. The return type of the method is
/// the output schema: the model answers in structured output and the strategy checks the answer against the type.
/// </summary>
/// <remarks>
/// <para>
/// Any type that System.Text.Json can read works as a return type: classes, records, scalars, lists, dictionaries.
/// A <c>Nullable&lt;T&gt;</c> is read as <c>T</c>. A type whose schema root is not an object travels as
/// <c>{"result": value}</c> (see <c>StructuredOutputParser</c>). A failed check goes back to the model as an
/// <see cref="Error"/> event and the strategy asks again, up to <see cref="PredictConfig.MaxRetries"/> attempts.
/// </para>
/// <para>
/// Differences from upstream are listed in <c>docs/porting-notes.md</c> (T14).
/// </para>
/// </remarks>
public sealed class PredictStrategy : GenerationStrategy
{
    private const string _strategyPromptKey = "strategy_prompt";

    private const string _strategyInstructions =
        """
        You are generating structured data matching the specified return type.
        Output will be validated against the type schema.

        Ensure your response:
        1. Includes all required fields
        2. Uses correct types for each field
        3. Follows the exact schema structure
        4. Contains no extra fields beyond the schema

        The LLM will automatically format your output as JSON.
        """;

    /// <summary>Creates the strategy with the default config.</summary>
    public PredictStrategy()
        : this(new PredictConfig())
    {
    }

    /// <summary>Creates the strategy.</summary>
    /// <param name="config">Retry limits, size guards and output serialization.</param>
    /// <exception cref="ConfigValidationException">The config is not valid.</exception>
    public PredictStrategy(PredictConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        Config = config;
    }

    /// <summary>The config of this strategy.</summary>
    public PredictConfig Config { get; }

    /// <inheritdoc />
    public override string Name => "PREDICT";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object?>? GetBlockOverrides() =>
        new Dictionary<string, object?> { [_strategyPromptKey] = new DynamicContext(() => _strategyInstructions) };

    /// <inheritdoc />
    /// <exception cref="GenerationException">The method has no return type, the type has no JSON schema, or the model fails the validation on every attempt.</exception>
    /// <exception cref="ArgumentException">An argument is larger than <see cref="PredictConfig.MaxParamChars"/>.</exception>
    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(call);

        // Observability consumers detect this single-shot turn through the pair: turn 1, final.
        var generationId = runtime.GenerationId ?? "";
        var parentGenerationId = runtime.ParentGenerationId;
        runtime.EventManager.Add(
            new BeforeTurn { MethodName = call.MethodName, Strategy = Name, GenerationId = generationId, ParentGenerationId = parentGenerationId, TurnNumber = 1 },
            record: false);
        var success = false;
        string? exceptionType = null;
        try
        {
            var result = await PredictAsync(runtime, call, cancellationToken).ConfigureAwait(false);
            success = true;
            return result;
        }
        catch (Exception e)
        {
            exceptionType = e.GetType().Name;
            throw;
        }
        finally
        {
            runtime.EventManager.Add(
                new AfterTurn
                {
                    MethodName = call.MethodName,
                    Strategy = Name,
                    GenerationId = generationId,
                    ParentGenerationId = parentGenerationId,
                    TurnNumber = 1,
                    IsFinal = true,
                    Success = success,
                    ExceptionType = exceptionType,
                },
                record: false);
        }
    }

    private static string TaskMessage(CurrentCall call) =>
        $"# Your task\n{call.Docstring}\n\n## Input parameters:\n{call.FormatParametersAsCode()}\n\nPerform the task and return the result directly.";

    private static string PydanticValidationMessage(string errorList) =>
        $"""
        Your output failed validation. Please fix the following errors:

        {errorList}

        Ensure your JSON output:
        1. Includes all required fields
        2. Uses correct types for each field
        3. Follows the exact schema structure
        4. Contains no extra fields

        Try again with corrected output.
        """;

    private static string TypeValidationMessage(string returnType, string error) =>
        $"""
        Your output failed type validation for return type `{returnType}`.

        Error: {error}

        Ensure your JSON output:
        1. Matches the expected type structure
        2. Contains valid data for the specified type
        3. Is properly formatted JSON

        Try again with corrected output.
        """;

    private static string JsonParsingMessage(string error) =>
        $"""
        Your output could not be parsed as JSON.

        Error: {error}

        Requirements:
        1. Output ONLY valid JSON - no markdown, no code fences, no explanatory text
        2. Ensure proper JSON syntax (quotes around strings, commas between items)
        3. Do not include any text before or after the JSON object

        Try again with valid JSON output.
        """;

    private static string FormatValidationError(ValidationException error, Type returnType) => error switch
    {
        StructuredOutputException { IsMalformedJson: true } malformed => JsonParsingMessage(malformed.Detail),
        StructuredOutputException { Path: { } path } shaped => PydanticValidationMessage($"  - Field `{path}`: {shaped.Detail}"),
        StructuredOutputException other => TypeValidationMessage(TypeNameFormatter.Format(returnType), other.Detail),
        _ => TypeValidationMessage(TypeNameFormatter.Format(returnType), error.Message),
    };

    private static object ParseResponse(LlmResponse response, Type returnType, bool wrapped) =>
        response.Parsed is { } parsed && returnType.IsInstanceOfType(parsed)
            ? parsed
            : StructuredOutputParser.Parse(response, returnType, wrapped);

    private static string? RawContent(ValidationException error) => error switch
    {
        StructuredOutputException { Content.Length: 0 } => "(empty response - both content and reasoning were empty)",
        StructuredOutputException e => e.Content,
        _ => null,
    };

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        // Do not cut a surrogate pair in half.
        var cut = max > 0 && char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..cut] + $"\n... (truncated, {text.Length} chars total)";
    }

    private static string Group(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private async Task<object?> PredictAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        var declared = call.ReturnType
            ?? throw new GenerationException(
                $"Method `{call.MethodName}` has no return type. PREDICT strategy requires a return type (a class, a record or a basic type).");
        var returnType = Nullable.GetUnderlyingType(declared) ?? declared;

        bool wrapped;
        try
        {
            wrapped = StructuredOutputParser.RequiresWrapper(ToolSchema.FromType(returnType));
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException or JsonException)
        {
            throw new GenerationException(
                $"PredictStrategy cannot build a JSON schema for return type {TypeNameFormatter.Format(returnType)}: {e.Message}\n"
                + "PredictStrategy requires a JSON-serializable return type. Use CodeActStrategy (which can build and return "
                + "the object in code), or return a serializable proxy.",
                e);
        }

        // A single-shot call with a truncated input gives silently wrong output, so an oversized input fails loudly.
        AssertParamSizes(call);

        runtime.EventManager.Add(new TaskEvent { Prompt = TaskMessage(call) });

        for (var attempt = 1; attempt <= Config.MaxRetries; attempt++)
        {
            try
            {
                var messages = runtime.BuildMessages(Config.MaxTokens);
                var generated = await runtime.GenerateAsync(messages, tools: null, returnType, cancellationToken).ConfigureAwait(false);
                var value = ParseResponse(generated.Response, returnType, wrapped);
                if (Config.OutputSerialization == PredictOutputSerialization.ToolCall)
                {
                    AppendToolCall(runtime, value, returnType);
                }

                return value;
            }
            catch (ValidationException e)
            {
                var details = FormatValidationError(e, returnType);
                if (attempt >= Config.MaxRetries)
                {
                    throw new GenerationException(
                        $"Structured output validation failed after {Config.MaxRetries} attempts.\nLast error:\n{details}", e);
                }

                var content = $"Validation error (attempt {attempt}/{Config.MaxRetries}):\n{details}";
                if (RawContent(e) is { } raw)
                {
                    content += $"\n\nRaw LLM output:\n{Truncate(raw, Config.MaxErrorChars)}";
                }

                runtime.EventManager.Add(new Error { Content = content });
            }
        }

        throw new GenerationException($"Structured output generation failed after {Config.MaxRetries} attempts");
    }

    internal void AssertParamSizes(CurrentCall call)
    {
        if (Config.MaxParamChars is not { } limit)
        {
            return;
        }

        foreach (var argument in call.Arguments)
        {
            // A string that is already over the limit has an oversized rendering too: skip the rendering.
            if (argument.Value is string text && text.Length > limit)
            {
                throw new ArgumentException(
                    $"PredictStrategy: parameter '{argument.Name}' is {Group(text.Length)} chars, exceeding MaxParamChars={Group(limit)}. "
                    + "Chunk or summarise the input before calling this method, "
                    + "or raise PredictConfig.MaxParamChars if the size is intentional.");
            }

            using var writer = new TruncatingTextWriter(limit);
            PFormat.Write(writer, argument.Value);
            if (writer.WasTruncated)
            {
                throw new ArgumentException(
                    $"PredictStrategy: parameter '{argument.Name}' is {Group(writer.CharsWritten)} chars (rendered), exceeding MaxParamChars={Group(limit)}. "
                    + "Chunk or summarise the input before calling this method, "
                    + "or raise PredictConfig.MaxParamChars if the size is intentional.");
            }
        }
    }

    /// <summary>Appends a synthetic <c>return_result</c> call without replacing the provider turn.</summary>
    private static void AppendToolCall(IRuntimeServices runtime, object value, Type returnType)
    {
        var toolCallId = "predict_" + Guid.NewGuid().ToString("N")[..8];
        runtime.EventManager.Add(new ToolCallEvent
        {
            ToolCallId = toolCallId,
            Name = "return_result",
            Arguments = new JsonObject { ["result"] = JsonSerializer.SerializeToNode(value, returnType, NooaJson.Options) },
            Result = new ToolResult { ToolCallId = toolCallId, Content = "Result accepted.", ResultStatus = ResultStatus.Complete },
            Metadata = new JsonObject { ["synthetic"] = true, ["synthetic_type"] = "predict_return_result" },
        });
    }
}
