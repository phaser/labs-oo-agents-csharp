// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/formatter.py

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Nooa.Events;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Turns event blocks into the messages of a model request. Upstream splits this into a neutral
/// <c>RenderedMessage</c> list and a provider formatter. C# builds the <see cref="LlmMessage"/> list directly
/// (the neutral layer served the Python journal publisher, which is part of tracing, T19).
/// </summary>
internal static class BlockMessages
{
    private const string _noResult = "(no result recorded)";

    /// <summary>
    /// The shared body of the XML and Markdown formatters: one system message with all system blocks (empty when
    /// there are none), then the event messages. Empty input gives no message.
    /// </summary>
    public static IReadOnlyList<LlmMessage> Build(
        IReadOnlyList<ResolvedBlock> blocks,
        Func<ResolvedBlock, string> wrapSystem,
        Func<ResolvedBlock, string> wrapMessage,
        ILogger logger)
    {
        if (blocks.Count == 0)
        {
            return [];
        }

        var messages = new List<LlmMessage>
        {
            LlmMessage.System(string.Join("\n\n", blocks.Where(b => b.Role == Role.System).Select(wrapSystem))),
        };
        messages.AddRange(FromEventBlocks(blocks.Where(b => b.Role != Role.System), wrapMessage, logger));
        return messages;
    }

    /// <summary>
    /// Projects the event blocks into conversation turns. <see cref="LlmResponse"/> is the canonical assistant turn.
    /// A linked <see cref="ToolCallEvent"/> is an execution of a call from that turn and not a turn of its own, so the
    /// response and its results are emitted together, and only when the batch is complete.
    /// </summary>
    /// <param name="blocks">The event blocks. Their content is already serialized.</param>
    /// <param name="wrap">Wraps the content of a tagged non-tool event, or null for no wrapping.</param>
    /// <param name="logger">Receives a warning when a turn is left out.</param>
    public static List<LlmMessage> FromEventBlocks(IEnumerable<ResolvedBlock> blocks, Func<ResolvedBlock, string>? wrap, ILogger logger)
    {
        var all = blocks.ToList();
        var replayableTurnIds = all
            .Select(b => b.Event)
            .OfType<LlmResponse>()
            .Where(r => r.ReplayToolCalls.Count > 0)
            .Select(r => r.Id)
            .ToHashSet();
        var executions = new Dictionary<string, Dictionary<string, ToolCallEvent>>();
        foreach (var call in all.Select(b => b.Event).OfType<ToolCallEvent>())
        {
            if (call.LlmResponseId is { } responseId && replayableTurnIds.Contains(responseId))
            {
                if (!executions.TryGetValue(responseId, out var byCallId))
                {
                    executions[responseId] = byCallId = [];
                }

                byCallId[call.ToolCallId] = call;
            }
        }

        var messages = new List<LlmMessage>();
        foreach (var block in all)
        {
            if (block.Event is LlmResponse { ReplayToolCalls.Count: > 0 } response)
            {
                var byCallId = executions.GetValueOrDefault(response.Id) ?? [];
                var calls = response.ReplayToolCalls;
                if (calls.Select(c => c.Id).Distinct().Count() != calls.Count
                    || calls.Any(c => !byCallId.TryGetValue(c.Id, out var run) || run.Result is null))
                {
                    logger.LogWarning(
                        "LlmResponse {ResponseId} has an incomplete visible execution batch: the assistant tool-call turn is left out of the replay.",
                        response.Id);
                    continue;
                }

                messages.Add(new ResponseMessage(response));
                messages.AddRange(calls.Select(c => ToolResultMessage(byCallId[c.Id], logger)));
                continue;
            }

            // A linked execution is not an independent turn. If its response is filtered out or its batch is
            // incomplete, leave it out: history is never rebuilt from the sidecar.
            if (block.Event is ToolCallEvent { LlmResponseId: not null })
            {
                continue;
            }

            messages.AddRange(EventBlockToMessages(block, wrap, logger));
        }

        return messages;
    }

    private static IEnumerable<LlmMessage> EventBlockToMessages(ResolvedBlock block, Func<ResolvedBlock, string>? wrap, ILogger logger)
    {
        switch (block.Event)
        {
            case LlmResponse response:
                if (response.IsEmpty)
                {
                    return [];
                }

                // Malformed or incomplete calls stay observable but are not executable history: replay a text-only turn.
                return response.ToolCalls.Count == 0
                    ? [new ResponseMessage(response)]
                    : [new JsonMessage(AssistantMessage.Build(response.Content, [], response.Reasoning))];
            case ToolCallEvent call:
                var toolCall = new ToolCall { Id = call.ToolCallId, Name = call.Name, Arguments = call.Arguments.ToJsonString() };
                return [new JsonMessage(AssistantMessage.Build(null, [toolCall])), ToolResultMessage(call, logger)];
        }

        // Runtime and metadata events are recorded for traces and are never sent to the model.
        if (block.Role is Role.RuntimeEvent or Role.Metadata)
        {
            return [];
        }

        var content = wrap is not null && block.Metadata.Tag is not null ? wrap(block) : block.Content;
        var message = new JsonObject { ["role"] = BlockWrapper.RoleName(block.Role) };
        var images = ImagesOf(block.Event);
        if (images.Count > 0)
        {
            var parts = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = content });
            foreach (var image in images)
            {
                parts.Add(image.DeepClone());
            }

            message["content"] = parts;
        }
        else
        {
            message["content"] = content;
        }

        return [new JsonMessage(message)];
    }

    private static LlmMessage ToolResultMessage(ToolCallEvent call, ILogger logger)
    {
        if (call.Result is null)
        {
            logger.LogWarning(
                "ToolCallEvent {ToolCallId} has no result: a placeholder tool result prevents a corrupt context.", call.ToolCallId);
        }

        return LlmMessage.Tool(call.ToolCallId, call.Result?.Content ?? _noResult);
    }

    private static IReadOnlyList<JsonNode> ImagesOf(EventBase? evt) => evt switch
    {
        TaskEvent task => task.Images,
        CellOutput cell => cell.Images,
        _ => [],
    };
}
