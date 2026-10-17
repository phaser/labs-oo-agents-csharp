// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Nooa.Llm;

/// <summary>
/// An <see cref="ILlmClient"/> over any <see cref="IChatClient"/>, with tool schemas, structured output, retry,
/// a per-attempt deadline, usage normalization and optional <c>&lt;think&gt;</c> tag extraction.
/// </summary>
/// <remarks>
/// The client logs retries and request metadata only. It never logs message content, tool arguments or keys.
/// The API key belongs to the wrapped chat client. Anthropic and Responses adapters, native reasoning replay and
/// cache breakpoints are backlog B09.
/// </remarks>
public sealed partial class ChatClientLlmClient : ILlmClient
{
    private const int _maxSchemaNameLength = 64;
    private const string _strictKey = "strict";

    private readonly IChatClient _chatClient;
    private readonly ChatClientLlmOptions _options;
    private readonly RetryPolicy? _retry;
    private readonly ILogger? _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="chatClient">The chat client to call.</param>
    /// <param name="options">The settings. Null uses the defaults.</param>
    /// <param name="retry">The retry policy. Null builds one from <see cref="ChatClientLlmOptions.Retry"/>.</param>
    /// <param name="logger">Receives retry warnings and request metadata, never message content.</param>
    public ChatClientLlmClient(
        IChatClient chatClient,
        ChatClientLlmOptions? options = null,
        RetryPolicy? retry = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        _options = options ?? new ChatClientLlmOptions();
        _options.Validate();
        _chatClient = chatClient;
        _logger = logger;
        _retry = retry ?? (_options.Retry is null ? null : new RetryPolicy(_options.Retry, logger: logger));
    }

    /// <inheritdoc />
    public int? ContextWindow => _options.ContextWindow;

    /// <inheritdoc />
    public int CountTokens(string text) => TokenCounter.CharApproximate(text);

    /// <inheritdoc />
    public async Task<LlmResponse> CallAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var chatMessages = ChatMessageMapper.ToChatMessages(messages);
        var chatOptions = new ChatOptions
        {
            ModelId = _options.Model,
            MaxOutputTokens = _options.MaxOutputTokens,
            Temperature = _options.Temperature,
            TopP = _options.TopP,
            Reasoning = _options.ReasoningEffort is { } effort ? new ReasoningOptions { Effort = effort } : null,
        };

        if (tools is { Count: > 0 })
        {
            chatOptions.Tools = tools.Select(t => (AITool)ToDeclaration(t)).ToList();
        }

        var wrapped = false;
        if (outputType is not null)
        {
            wrapped = ApplyOutputFormat(chatOptions, outputType);
        }

        var response = await ExecuteAsync(chatMessages, chatOptions, cancellationToken).ConfigureAwait(false);
        if (_logger is not null)
        {
            LogCompleted(_logger, response.FinishReason, response.Parts.Count, response.Usage?.TotalTokens ?? 0);
        }

        return outputType is null || response.ToolCalls.Count > 0 ? response : response with { Parsed = StructuredOutputParser.Parse(response, outputType, wrapped) };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLM call finished: {FinishReason}, {PartCount} parts, {TotalTokens} tokens")]
    private static partial void LogCompleted(ILogger logger, FinishReason finishReason, int partCount, int totalTokens);

    private static string SchemaName(Type type) =>
        Regex.Replace(type.Name, "[^a-zA-Z0-9_-]", "_") is { Length: > 0 } name
            ? name[..Math.Min(name.Length, _maxSchemaNameLength)]
            : "output";

    private Task<LlmResponse> ExecuteAsync(List<ChatMessage> messages, ChatOptions options, CancellationToken cancellationToken) =>
        _retry is null
            ? AttemptAsync(messages, options, cancellationToken)
            : _retry.ExecuteAsync(ct => AttemptAsync(messages, options, ct), cancellationToken);

    private async Task<LlmResponse> AttemptAsync(List<ChatMessage> messages, ChatOptions options, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.RequestTimeout is { } timeout)
        {
            deadline.CancelAfter(timeout);
        }

        ChatResponse raw;
        try
        {
            raw = await _chatClient.GetResponseAsync(messages, options, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("The LLM call exceeded the request timeout");
        }

        var response = ToLlmResponse(raw);
        if (_retry is { RetryOnEmptyContent: true }
            && response.Content.Trim().Length == 0
            && response.ToolCalls.Count == 0
            && response.Reasoning is { } reasoning)
        {
            throw new EmptyContentException(reasoning);
        }

        return response;
    }

    private LlmResponse ToLlmResponse(ChatResponse raw)
    {
        var parts = ChatMessageMapper.ToParts(raw);
        if (_options.ExtractThinkTags)
        {
            parts = ExtractThinkTags(parts);
        }

        var hasToolCalls = parts.OfType<ToolCall>().Any();
        return new LlmResponse
        {
            RawResponse = raw.RawRepresentation ?? raw,
            Parts = parts,
            FinishReason = ChatMessageMapper.ToFinishReason(raw.FinishReason, hasToolCalls),
            Usage = ChatMessageMapper.ToUsage(raw.Usage),
            ModelName = raw.ModelId ?? _options.Model ?? "",
        };
    }

    private static List<AssistantPart> ExtractThinkTags(List<AssistantPart> parts)
    {
        var result = new List<AssistantPart>();
        foreach (var part in parts)
        {
            if (part is not AssistantText text)
            {
                result.Add(part);
                continue;
            }

            var (content, reasoning) = ThinkTags.Extract(text.Text);
            if (reasoning is null)
            {
                result.Add(part);
                continue;
            }

            result.Add(new AssistantReasoning { Text = reasoning });
            if (content.Length > 0)
            {
                result.Add(text with { Text = content });
            }
        }

        return result;
    }

    private ToolDeclaration ToDeclaration(ToolDefinition tool)
    {
        var loose = ToolSchema.Clean(tool.ParametersSchema);
        var properties = loose["properties"] as JsonObject;
        var required = loose["required"] as JsonArray;
        var strict = _options.StrictTools && (required?.Count ?? 0) >= (properties?.Count ?? 0);
        JsonObject schema;
        if (strict)
        {
            schema = ToolSchema.Clean(tool.ParametersSchema, strict: true);
            if (!ToolSchema.IsStrictValid(schema))
            {
                schema = ToolSchema.Loose(loose);
                strict = false;
            }
        }
        else
        {
            schema = ToolSchema.Loose(loose);
        }

        return new ToolDeclaration(tool.Name, tool.Description, JsonSerializer.SerializeToElement(schema), strict);
    }

    // Returns true when the schema root is not an object. OpenAI needs an object root, so the value travels as
    // {"result": value} and Parse unwraps it.
    private static bool ApplyOutputFormat(ChatOptions chatOptions, Type outputType)
    {
        var raw = ToolSchema.FromType(outputType);
        var wrap = StructuredOutputParser.RequiresWrapper(raw);
        var root = raw;
        if (wrap)
        {
            root = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { [StructuredOutputParser.WrapperProperty] = raw.DeepClone() },
                ["required"] = new JsonArray(StructuredOutputParser.WrapperProperty),
            };
            if (root["properties"]![StructuredOutputParser.WrapperProperty]!.AsObject().Remove("$defs", out var defs))
            {
                root["$defs"] = defs;
            }
        }

        var strict = ToolSchema.IsStrictCompatible(ToolSchema.ResolveRefs(root));
        var schema = strict ? ToolSchema.Clean(root, strict: true) : ToolSchema.Loose(root);
        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
            JsonSerializer.SerializeToElement(schema), SchemaName(outputType));
        chatOptions.AdditionalProperties = new AdditionalPropertiesDictionary { [_strictKey] = strict };
        return wrap;
    }

    private sealed class ToolDeclaration : AIFunctionDeclaration
    {
        private readonly string _name;
        private readonly string _description;
        private readonly JsonElement _schema;
        private readonly IReadOnlyDictionary<string, object?> _additional;

        public ToolDeclaration(string name, string description, JsonElement schema, bool strict)
        {
            _name = name;
            _description = description;
            _schema = schema;
            _additional = new Dictionary<string, object?> { [_strictKey] = strict };
        }

        public override string Name => _name;

        public override string Description => _description;

        public override JsonElement JsonSchema => _schema;

        public override IReadOnlyDictionary<string, object?> AdditionalProperties => _additional;
    }
}
