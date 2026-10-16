// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.Json.Nodes;

namespace Nooa.Llm;

/// <summary>
/// One item in the input list of <see cref="ILlmClient.CallAsync"/>. Upstream the list holds OpenAI-style
/// dictionaries, <c>LLMResponse</c> events and <c>CacheBoundary</c> markers. C# uses a closed set of three
/// record types: <see cref="JsonMessage"/>, <see cref="ResponseMessage"/> and <see cref="CacheBoundary"/>.
/// </summary>
public abstract record LlmMessage
{
    /// <summary>Creates a <c>system</c> chat message.</summary>
    public static LlmMessage System(string content) => Chat("system", content);

    /// <summary>Creates a <c>user</c> chat message.</summary>
    public static LlmMessage User(string content) => Chat("user", content);

    /// <summary>Creates a <c>tool</c> chat message that answers the tool call with the given id.</summary>
    public static LlmMessage Tool(string toolCallId, string content) => new JsonMessage(new JsonObject
    {
        ["role"] = "tool",
        ["tool_call_id"] = toolCallId,
        ["content"] = content,
    });

    private static JsonMessage Chat(string role, string content) =>
        new(new JsonObject { ["role"] = role, ["content"] = content });
}
