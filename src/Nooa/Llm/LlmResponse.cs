// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Nooa.ContextBlocks;

namespace Nooa.Llm;

/// <summary>
/// Response produced by the LLM client and stored by NOOA. Persisted as <c>LLMResponse</c>.
/// </summary>
/// <remarks>
/// <para>
/// The client creates a fresh object for every call. The runtime records that same object. Renderers project the
/// conversational properties; telemetry reads the model and usage metadata.
/// </para>
/// <para>
/// Parts are immutable. Edit a turn with <see cref="ReplaceParts"/> or <see cref="ReplaceText"/>: they drop the
/// native state of the parts, the replay scope, <see cref="RawResponse"/> and <see cref="Parsed"/>.
/// A plain <c>with</c> expression on <see cref="Parts"/> keeps that state, so do not use it to edit parts.
/// </para>
/// </remarks>
public sealed record LlmResponse() : EventBase(Role.Assistant)
{
    /// <summary>
    /// Live provider SDK response. It is not persisted: it is provider-specific, may not serialize and
    /// duplicates the normalized properties.
    /// </summary>
    [JsonIgnore]
    [Hidden]
    public object? RawResponse { get; init; }

    /// <summary>The ordered assistant parts. Not shown to the model as parts; renderers use the projections.</summary>
    [Hidden]
    public IReadOnlyList<AssistantPart> Parts { get; init; } = [];

    /// <summary>Scope in which the native state of the parts can replay. Not shown to the model.</summary>
    [Hidden]
    public string? ReplayScope { get; init; }

    /// <summary>
    /// Live typed return value. It is not persisted: an arbitrary object is not a durable wire format
    /// (the source JSON remains in <see cref="Content"/> or in the reasoning).
    /// </summary>
    [JsonIgnore]
    [Hidden]
    public object? Parsed { get; init; }

    /// <summary>NOOA-normalized outcome. Not shown to the model.</summary>
    [Hidden]
    public FinishReason FinishReason { get; init; } = FinishReason.Stop;

    /// <summary>Normalized token, cache, reasoning and cost usage. Not shown to the model.</summary>
    [Hidden]
    public LlmUsage? Usage { get; init; }

    /// <summary>Model identifier used for this response. Not shown to the model.</summary>
    [Hidden]
    public string ModelName { get; init; } = "";

    /// <summary>Generation turn that produced this response. Not shown to the model.</summary>
    [Hidden]
    public string GenerationId { get; init; } = "";

    /// <summary>
    /// Snapshot of the trailing dynamic context block in the request that produced this response,
    /// kept for session export and debugging. Not shown to the model.
    /// </summary>
    [Hidden]
    public string DynamicContext { get; init; } = "";

    /// <summary>The visible text: all text parts, joined. The renderer must not truncate it.</summary>
    [JsonIgnore]
    public string Content => string.Concat(Parts.OfType<AssistantText>().Select(p => p.Text));

    /// <summary>The readable reasoning: all non-empty reasoning parts, joined with newlines, or null.</summary>
    [JsonIgnore]
    public string? Reasoning
    {
        get
        {
            var joined = string.Join('\n', Parts.OfType<AssistantReasoning>().Select(p => p.Text).Where(t => t.Length > 0));
            return joined.Length == 0 ? null : joined;
        }
    }

    /// <summary>The tool calls, in order.</summary>
    [JsonIgnore]
    public IReadOnlyList<ToolCall> ToolCalls => Parts.OfType<ToolCall>().ToList();

    /// <summary>
    /// The tool calls that history replay may use. Incomplete or malformed calls remain observable,
    /// but are not executable history: the list is empty unless the finish reason is <c>tool_calls</c>
    /// and every argument string is a JSON object.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ToolCall> ReplayToolCalls
    {
        get
        {
            var calls = ToolCalls;
            if (FinishReason != FinishReason.ToolCalls)
            {
                return [];
            }

            foreach (var call in calls)
            {
                try
                {
                    if (JsonNode.Parse(call.Arguments) is not JsonObject)
                    {
                        return [];
                    }
                }
                catch (JsonException)
                {
                    return [];
                }
            }

            return calls;
        }
    }

    /// <summary>Alias of <see cref="Content"/> for the formatter event-to-message interface.</summary>
    [JsonIgnore]
    public string ReplayContent => Content;

    /// <summary>
    /// True when the response has no replay text, no replayable tool call, no reasoning and no native state.
    /// </summary>
    [JsonIgnore]
    public override bool IsEmpty =>
        ReplayContent.Trim().Length == 0
        && ReplayToolCalls.Count == 0
        && Reasoning is null
        && !Parts.Any(p => p.Native is { Count: > 0 });

    /// <summary>
    /// Replaces the parts without inheriting private provider state. The metadata stays associated with the
    /// originating event, as a copy. The stored event is untouched; only the returned copy loses the opaque extensions.
    /// </summary>
    public LlmResponse ReplaceParts(IEnumerable<AssistantPart> parts) => this with
    {
        Parts = parts.Select(p => p with { Native = null }).ToList(),
        ReplayScope = null,
        RawResponse = null,
        Parsed = null,
        Metadata = (JsonObject)Metadata.DeepClone(),
    };

    /// <summary>Replaces the visible text. Readable reasoning and public tool calls stay.</summary>
    public LlmResponse ReplaceText(string text) =>
        ReplaceParts(Parts.Where(p => p is not AssistantText).Prepend(new AssistantText { Text = text }));

    /// <summary>
    /// The public, non-replay projection for relay, tracing and token counting. It is built on demand
    /// and never reads native state.
    /// </summary>
    public JsonObject PublicMessage() => AssistantMessage.Build(Content, ToolCalls, Reasoning);
}
