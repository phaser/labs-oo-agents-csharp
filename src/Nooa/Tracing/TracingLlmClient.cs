// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Nodes;
using Nooa.Llm;

namespace Nooa.Tracing;

/// <summary>
/// An <see cref="ILlmClient"/> decorator that records one LLM span for each call, with the OpenInference <c>llm.*</c> attributes.
/// Upstream gets these spans from the LiteLLM instrumentor; this decorator replaces it.
/// </summary>
/// <remarks>
/// <para>
/// Messages are scrubbed and bounded: at most <see cref="MaxMessages"/> input messages (the first half and the last half; the
/// number that is left out is in <c>nooa.llm.input_messages.omitted</c>), and each value is scrubbed in full, then capped by <c>SpanAttributes</c>.
/// Opaque provider state and reasoning text are not recorded. The span is skipped inside a <see cref="NoTraceAttribute"/> generation.
/// </para>
/// </remarks>
/// <param name="inner">The client to call.</param>
public sealed class TracingLlmClient(ILlmClient inner) : ILlmClient
{
    /// <summary>The largest number of input messages that one span records.</summary>
    public const int MaxMessages = 100;

    /// <inheritdoc />
    public int? ContextWindow => inner.ContextWindow;

    /// <inheritdoc />
    public int CountTokens(string text) => inner.CountTokens(text);

    /// <inheritdoc />
    public async Task<LlmResponse> CallAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null,
        CancellationToken cancellationToken = default)
    {
        var span = TraceSuppression.IsActive ? null : NooaTracing.Source.StartActivity("llm", ActivityKind.Client);
        if (span is null)
        {
            return await inner.CallAsync(messages, tools, outputType, cancellationToken);
        }

        try
        {
            SpanAttributes.Set(span, OpenInferenceAttributes.SpanKind, OpenInferenceAttributes.KindLlm);
            SpanAttributes.Set(span, OpenInferenceAttributes.SessionId, TraceSession.Current);
            RecordInput(span, messages, tools);
            var response = await inner.CallAsync(messages, tools, outputType, cancellationToken);
            RecordOutput(span, response);
            span.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            SpanAttributes.RecordError(span, ex);
            throw;
        }
        finally
        {
            span.Stop();
        }
    }

    private static void RecordInput(Activity span, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition>? tools)
    {
        var chat = messages.Where(m => m is not CacheBoundary).ToList();
        var omitted = Math.Max(0, chat.Count - MaxMessages);
        var kept = omitted == 0 ? chat : [.. chat.Take(MaxMessages / 2), .. chat.Skip(chat.Count - (MaxMessages / 2))];
        for (var i = 0; i < kept.Count; i++)
        {
            var prefix = $"{OpenInferenceAttributes.LlmInputMessages}.{i}.message";
            switch (kept[i])
            {
                case JsonMessage { Value: var json }:
                    SpanAttributes.Set(span, prefix + ".role", json["role"]?.ToString());
                    SpanAttributes.Set(span, prefix + ".content", Content(json["content"]));
                    SpanAttributes.Set(span, prefix + ".tool_call_id", json["tool_call_id"]?.ToString());
                    break;
                case ResponseMessage { Response: var response }:
                    SpanAttributes.Set(span, prefix + ".role", "assistant");
                    SpanAttributes.Set(span, prefix + ".content", response.Content);
                    RecordToolCalls(span, prefix, response.ToolCalls);
                    break;
            }
        }

        if (omitted > 0)
        {
            SpanAttributes.Set(span, "nooa.llm.input_messages.omitted", omitted);
        }

        for (var i = 0; i < (tools?.Count ?? 0); i++)
        {
            SpanAttributes.Set(span, $"llm.tools.{i}.tool.name", tools![i].Name);
        }
    }

    private static void RecordOutput(Activity span, LlmResponse response)
    {
        var prefix = $"{OpenInferenceAttributes.LlmOutputMessages}.0.message";
        SpanAttributes.Set(span, OpenInferenceAttributes.LlmModelName, response.ModelName);
        SpanAttributes.Set(span, prefix + ".role", "assistant");
        SpanAttributes.Set(span, prefix + ".content", response.Content);
        RecordToolCalls(span, prefix, response.ToolCalls);
        SpanAttributes.Set(span, OpenInferenceAttributes.OutputValue, response.Content);
        SpanAttributes.Set(span, OpenInferenceAttributes.OutputMimeType, OpenInferenceAttributes.MimeText);
        SpanAttributes.Set(span, "llm.finish_reason", response.FinishReason.ToString());
        if (response.Usage is { } usage)
        {
            SpanAttributes.Set(span, OpenInferenceAttributes.TokenCountPrompt, usage.InputTokens);
            SpanAttributes.Set(span, OpenInferenceAttributes.TokenCountCompletion, usage.OutputTokens);
            SpanAttributes.Set(span, OpenInferenceAttributes.TokenCountTotal, usage.TotalTokens);
            SpanAttributes.Set(span, OpenInferenceAttributes.TokenCountCacheRead, usage.CachedInputTokens);
            SpanAttributes.Set(span, OpenInferenceAttributes.TokenCountCacheWrite, usage.CacheWriteInputTokens);
            SpanAttributes.Set(span, OpenInferenceAttributes.TokenCountReasoning, usage.ReasoningTokens);
            SpanAttributes.Set(span, "llm.cost_usd", usage.CostUsd);
        }
    }

    private static void RecordToolCalls(Activity span, string messagePrefix, IReadOnlyList<ToolCall> calls)
    {
        for (var i = 0; i < calls.Count; i++)
        {
            var prefix = $"{messagePrefix}.tool_calls.{i}.tool_call";
            SpanAttributes.Set(span, prefix + ".id", calls[i].Id);
            SpanAttributes.Set(span, prefix + ".function.name", calls[i].Name);
            SpanAttributes.Set(span, prefix + ".function.arguments", calls[i].Arguments);
        }
    }

    private static string? Content(JsonNode? content) => content switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => content.ToJsonString(),
    };
}
