// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/fake.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Json;

namespace Nooa.Llm;

/// <summary>
/// Fake LLM client that returns scripted responses, for hermetic tests without network calls.
/// Script it with the <c>Enqueue</c> methods (they chain), then inspect <see cref="Calls"/>.
/// Concurrent calls are safe: they take responses and transcript indexes in order.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<LlmResponse> _responses = new();
    private readonly HashSet<LlmResponse> _enqueued = new(ReferenceEqualityComparer.Instance);
    private readonly List<FakeLlmCall> _calls = [];
    private readonly bool _strictExhaustion;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<LlmMessage> _lastMessages = [];
    private IReadOnlyList<ToolDefinition>? _lastTools;

    /// <summary>Creates a fake client.</summary>
    /// <param name="scriptedResponses">Responses to return, in order.</param>
    /// <param name="strictExhaustion">
    /// Throw <see cref="FakeLlmResponseExhaustedException"/> when a call has no scripted response,
    /// instead of returning an empty response.
    /// </param>
    public FakeLlmClient(IEnumerable<LlmResponse>? scriptedResponses = null, bool strictExhaustion = false)
    {
        _strictExhaustion = strictExhaustion;
        foreach (var response in scriptedResponses ?? [])
        {
            Enqueue(response);
        }
    }

    /// <summary>The fake context window, 128000 tokens.</summary>
    public int? ContextWindow => 128_000;

    /// <summary>The number of calls made since construction or the last <see cref="Reset"/>.</summary>
    public int CallCount { get; private set; }

    /// <summary>The messages of the latest call.</summary>
    public IReadOnlyList<LlmMessage> LastMessages => _lastMessages;

    /// <summary>The tools of the latest call, or null.</summary>
    public IReadOnlyList<ToolDefinition>? LastTools => _lastTools;

    /// <summary>The ordered, read-only transcript of the calls made so far.</summary>
    public IReadOnlyList<FakeLlmCall> Calls
    {
        get
        {
            lock (_calls)
            {
                return _calls.ToArray();
            }
        }
    }

    /// <summary>The number of scripted responses that no call has consumed.</summary>
    public int RemainingResponses
    {
        get
        {
            lock (_responses)
            {
                return _responses.Count;
            }
        }
    }

    /// <summary>Creates a client that returns one text response.</summary>
    public static FakeLlmClient SimpleMessage(string message, bool strictExhaustion = false) =>
        new FakeLlmClient(strictExhaustion: strictExhaustion).EnqueueText(message);

    /// <summary>Creates a client that returns one response per code string, in order.</summary>
    public static FakeLlmClient WithCodeResponses(IEnumerable<string> codeStrings, bool strictExhaustion = false)
    {
        var client = new FakeLlmClient(strictExhaustion: strictExhaustion);
        foreach (var code in codeStrings)
        {
            client.EnqueueText(code);
        }

        return client;
    }

    /// <summary>Creates a client that returns one tool call, with optional text before it.</summary>
    public static FakeLlmClient WithToolCall(
        string toolName,
        object? toolArguments = null,
        string? message = null,
        bool strictExhaustion = false) =>
        new FakeLlmClient(strictExhaustion: strictExhaustion).EnqueueToolCall(toolName, toolArguments, message);

    /// <summary>Creates a client that returns reasoning and a message (o1 style).</summary>
    public static FakeLlmClient WithReasoning(string reasoning, string message, bool strictExhaustion = false) =>
        new FakeLlmClient(strictExhaustion: strictExhaustion).EnqueueReasoning(reasoning, message);

    /// <summary>
    /// Adds a scripted response. A response object that is added a second time (the <c>[r] * n</c> shorthand)
    /// becomes a distinct event copy with its own id, as upstream does.
    /// </summary>
    public FakeLlmClient Enqueue(LlmResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        lock (_responses)
        {
            if (!_enqueued.Add(response))
            {
                response = response with
                {
                    Id = Guid.NewGuid().ToString(),
                    Metadata = (JsonObject)response.Metadata.DeepClone(),
                    Tag = null,
                    Timestamp = DateTimeOffset.Now,
                };
                _enqueued.Add(response);
            }

            _responses.Enqueue(response);
        }

        return this;
    }

    /// <summary>Adds a text response. Usage is 10 input tokens and one output token per word.</summary>
    public FakeLlmClient EnqueueText(string text) =>
        Enqueue(Text(text, [], FinishReason.Stop, Usage(10, WordCount(text))));

    /// <summary>
    /// Adds a response with one tool call and optional text. The arguments object is serialized to JSON
    /// (null becomes <c>{}</c>). The finish reason is <c>tool_calls</c>.
    /// </summary>
    public FakeLlmClient EnqueueToolCall(string toolName, object? arguments = null, string? message = null, string id = "call_fake_123")
    {
        var call = new ToolCall
        {
            Id = id,
            Name = toolName,
            Arguments = arguments is null ? "{}" : JsonSerializer.Serialize(arguments),
        };
        return Enqueue(Text(message ?? "", [call], FinishReason.ToolCalls, Usage(10, 5)));
    }

    /// <summary>Adds a reasoning and text response.</summary>
    public FakeLlmClient EnqueueReasoning(string reasoning, string message) =>
        Enqueue(Text(message, [new AssistantReasoning { Text = reasoning }], FinishReason.Stop, Usage(15, 10)));

    /// <summary>
    /// Adds a structured-output response: the content is the value as JSON and <see cref="LlmResponse.Parsed"/>
    /// is the value itself.
    /// </summary>
    public FakeLlmClient EnqueueStructured<T>(T value) =>
        Enqueue(Text(JsonSerializer.Serialize(value, NooaJson.Options), [], FinishReason.Stop, Usage(10, 5)) with
        {
            Parsed = value,
        });

    /// <inheritdoc />
    public int CountTokens(string text) => text.EnumerateRunes().Count() / 4 + 1;

    /// <inheritdoc />
    public async Task<LlmResponse> CallAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return ScriptedCall(messages, tools, outputType);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Clears the call history. Scripted responses that are already consumed stay consumed.</summary>
    public void Reset()
    {
        lock (_calls)
        {
            CallCount = 0;
            _lastMessages = [];
            _lastTools = null;
            _calls.Clear();
        }
    }

    private static int WordCount(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static LlmUsage Usage(int input, int output) =>
        new() { InputTokens = input, OutputTokens = output, TotalTokens = input + output };

    private static LlmResponse Text(string text, IEnumerable<AssistantPart> extra, FinishReason reason, LlmUsage usage) =>
        new()
        {
            Parts = (text.Length > 0 ? new AssistantPart[] { new AssistantText { Text = text } } : [])
                .Concat(extra).ToList(),
            FinishReason = reason,
            Usage = usage,
        };

    private static IReadOnlyList<LlmMessage> Snapshot(IReadOnlyList<LlmMessage> messages) =>
        messages.Select(m => m is JsonMessage j ? new JsonMessage((JsonObject)j.Value.DeepClone()) : m).ToArray();

    private static IReadOnlyList<ToolDefinition>? Snapshot(IReadOnlyList<ToolDefinition>? tools) =>
        tools?.Select(t => t with { ParametersSchema = (JsonObject)t.ParametersSchema.DeepClone() }).ToArray();

    private LlmResponse ScriptedCall(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition>? tools, Type? outputType)
    {
        LlmResponse? response = null;
        Exception? error = null;
        var messagesCopy = Snapshot(messages);
        var toolsCopy = Snapshot(tools);
        lock (_calls)
        {
            CallCount++;
            _lastMessages = messagesCopy;
            _lastTools = toolsCopy;
            lock (_responses)
            {
                if (_responses.Count > 0)
                {
                    response = _responses.Dequeue();
                }
                else if (_strictExhaustion)
                {
                    error = new FakeLlmResponseExhaustedException(
                        $"FakeLlmClient has no scripted response for call {CallCount}; "
                        + "add a response or disable strictExhaustion");
                }
                else
                {
                    response = new LlmResponse { FinishReason = FinishReason.Stop };
                }
            }

            _calls.Add(new FakeLlmCall(
                CallCount,
                messagesCopy,
                toolsCopy,
                outputType,
                response is null ? null : response with { Metadata = (JsonObject)response.Metadata.DeepClone() },
                error));
        }

        return error is null ? response! : throw error;
    }
}
