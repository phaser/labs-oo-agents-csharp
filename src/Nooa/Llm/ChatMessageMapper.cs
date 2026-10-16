// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Nooa.Json;
using ChatToolCall = OpenAI.Chat.ChatToolCall;

namespace Nooa.Llm;

/// <summary>Maps NOOA messages and responses to and from Microsoft.Extensions.AI content.</summary>
internal static class ChatMessageMapper
{
    /// <summary>
    /// Maps the request messages. A <see cref="CacheBoundary"/> is consumed: OpenAI-compatible endpoints cache
    /// a stable prefix on their own, so the marker has no wire form. More than one marker is an error, as upstream.
    /// </summary>
    public static List<ChatMessage> ToChatMessages(IReadOnlyList<LlmMessage> messages)
    {
        var result = new List<ChatMessage>();
        var boundaries = 0;
        foreach (var message in messages)
        {
            switch (message)
            {
                case CacheBoundary:
                    if (++boundaries > 1)
                    {
                        throw new ArgumentException("The messages contain more than one cache boundary", nameof(messages));
                    }

                    break;
                case JsonMessage json:
                    result.Add(FromJson(json.Value));
                    break;
                case ResponseMessage response:
                    result.Add(FromResponse(response.Response));
                    break;
                default:
                    throw new ArgumentException($"Unsupported message type {message.GetType().Name}", nameof(messages));
            }
        }

        return result;
    }

    /// <summary>Maps the parts of a chat response to NOOA assistant parts, in order.</summary>
    public static List<AssistantPart> ToParts(ChatResponse response)
    {
        var parts = new List<AssistantPart>();
        foreach (var content in response.Messages.SelectMany(m => m.Contents))
        {
            switch (content)
            {
                case TextReasoningContent reasoning:
                    parts.Add(new AssistantReasoning { Text = reasoning.Text ?? "", Native = Native(reasoning.RawRepresentation) });
                    break;
                case TextContent text:
                    parts.Add(new AssistantText { Text = text.Text ?? "", Native = Native(text.RawRepresentation) });
                    break;
                case FunctionCallContent call:
                    parts.Add(new ToolCall
                    {
                        Id = call.CallId,
                        Name = call.Name,
                        Arguments = ArgumentsOf(call),
                        Native = Native(call.RawRepresentation),
                    });
                    break;
            }
        }

        return parts;
    }

    /// <summary>Maps the finish reason. Truncation and failure win over tool calls, as upstream.</summary>
    public static FinishReason ToFinishReason(ChatFinishReason? reason, bool hasToolCalls)
    {
        if (reason == ChatFinishReason.Length)
        {
            return FinishReason.Length;
        }

        if (reason == ChatFinishReason.ContentFilter)
        {
            return FinishReason.Error;
        }

        return hasToolCalls || reason == ChatFinishReason.ToolCalls ? FinishReason.ToolCalls : FinishReason.Stop;
    }

    /// <summary>Normalizes the usage through <see cref="LlmUsage.FromProvider"/>.</summary>
    public static LlmUsage? ToUsage(UsageDetails? usage)
    {
        if (usage is null)
        {
            return null;
        }

        var json = new JsonObject();
        Set(json, "input_tokens", usage.InputTokenCount);
        Set(json, "output_tokens", usage.OutputTokenCount);
        Set(json, "total_tokens", usage.TotalTokenCount);
        Set(json, "cached_input_tokens", usage.CachedInputTokenCount);
        Set(json, "reasoning_tokens", usage.ReasoningTokenCount);
        return LlmUsage.FromProvider(json);
    }

    private static void Set(JsonObject json, string key, long? value)
    {
        if (value is { } v)
        {
            json[key] = v;
        }
    }

    private static ChatMessage FromResponse(LlmResponse response)
    {
        // Portable replay: native state replay is backlog B09. Only replayable tool calls are sent, as upstream.
        var contents = new List<AIContent>();
        if (response.Content.Length > 0)
        {
            contents.Add(new TextContent(response.Content));
        }

        contents.AddRange(response.ReplayToolCalls.Select(c => ToFunctionCall(c.Id, c.Name, c.Arguments)));
        return new ChatMessage(ChatRole.Assistant, contents);
    }

    private static ChatMessage FromJson(JsonObject message)
    {
        var role = message["role"]?.GetValue<string>()
            ?? throw new ArgumentException("A message has no role");
        switch (role)
        {
            case "system":
                return new ChatMessage(ChatRole.System, TextOf(message["content"]));
            case "user":
                return new ChatMessage(ChatRole.User, TextOf(message["content"]));
            case "tool":
                var callId = message["tool_call_id"]?.GetValue<string>()
                    ?? throw new ArgumentException("A tool message has no tool_call_id");
                return new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, TextOf(message["content"]))]);
            case "assistant":
                var contents = new List<AIContent>();
                var text = TextOf(message["content"]);
                if (text.Length > 0)
                {
                    contents.Add(new TextContent(text));
                }

                if (message["tool_calls"] is JsonArray calls)
                {
                    foreach (var call in calls)
                    {
                        var function = call?["function"] ?? throw new ArgumentException("A tool call has no function");
                        contents.Add(ToFunctionCall(
                            call["id"]?.GetValue<string>() ?? "",
                            function["name"]?.GetValue<string>() ?? "",
                            function["arguments"]?.GetValue<string>() ?? "{}"));
                    }
                }

                return new ChatMessage(ChatRole.Assistant, contents);
            default:
                throw new ArgumentException($"Unsupported message role '{role}'");
        }
    }

    // Content is a string or a list of text parts. Other part types have no mapping yet.
    private static string TextOf(JsonNode? content)
    {
        switch (content)
        {
            case null:
                return "";
            case JsonArray parts:
                return string.Concat(parts.Select(part =>
                    part?["type"]?.GetValue<string>() == "text"
                        ? part["text"]?.GetValue<string>() ?? ""
                        : throw new ArgumentException("Only text content parts are supported")));
            default:
                return content.GetValue<string>();
        }
    }

    private static FunctionCallContent ToFunctionCall(string id, string name, string arguments)
    {
        Dictionary<string, object?>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments, NooaJson.Options);
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"The arguments of tool call '{name}' are not a JSON object", e);
        }

        return new FunctionCallContent(id, name, parsed ?? throw new ArgumentException($"The arguments of tool call '{name}' are null"));
    }

    // The abstraction parses the arguments. The OpenAI raw call keeps the exact string the model wrote.
    private static string ArgumentsOf(FunctionCallContent call)
    {
        if (call.RawRepresentation is ChatToolCall raw)
        {
            return raw.FunctionArguments.ToString();
        }

        return call.Arguments is null ? "" : JsonSerializer.Serialize(call.Arguments, NooaJson.Options);
    }

    // The provider-native model as JSON, when the SDK object can write itself. Replay of it is backlog B09.
    private static NativeJson? Native(object? raw)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(ModelReaderWriter.Write(raw, ModelReaderWriterOptions.Json).ToString()) is JsonObject obj
                && obj.Count > 0
                ? new NativeJson(obj)
                : null;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
