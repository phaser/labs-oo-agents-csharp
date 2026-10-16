// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

using System.Text.Json.Nodes;

namespace Nooa.Llm;

/// <summary>Builds the public assistant message shape, used for rendering and JSON integrations.</summary>
public static class AssistantMessage
{
    /// <summary>
    /// Builds the public assistant message. Only public values enter the projection; native state is never read.
    /// Argument strings stay byte for byte intact. Absent text becomes an empty string.
    /// </summary>
    public static JsonObject Build(string? content, IEnumerable<ToolCall> toolCalls, string? reasoning = null)
    {
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = content ?? "",
        };
        if (!string.IsNullOrEmpty(reasoning))
        {
            message["reasoning_content"] = reasoning;
        }

        var calls = new JsonArray();
        foreach (var call in toolCalls)
        {
            calls.Add(new JsonObject
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = call.Name,
                    ["arguments"] = call.Arguments,
                },
            });
        }

        if (calls.Count > 0)
        {
            message["tool_calls"] = calls;
        }

        return message;
    }
}
