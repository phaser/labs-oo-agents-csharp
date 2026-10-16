// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class ToolCallEventTests
{
    [Fact]
    public void RoundTrip_WithoutResult_ResultIsNull()
    {
        var e = new ToolCallEvent { ToolCallId = "call_abc", Name = "get_data", Arguments = new JsonObject { ["key"] = "value" } };

        var back = EventJson.RoundTrip(e);

        Assert.Equal("ToolCallEvent", back.EventType);
        Assert.Equal("call_abc", back.ToolCallId);
        Assert.Equal("get_data", back.Name);
        Assert.Equal("value", (string?)back.Arguments["key"]);
        Assert.Null(back.Result);
        Assert.Null(back.LlmResponseId);
    }

    [Fact]
    public void RoundTrip_WithNestedResult_KeepsResult()
    {
        var e = new ToolCallEvent
        {
            ToolCallId = "call_weather_123",
            Name = "get_weather",
            Arguments = new JsonObject { ["location"] = "SF" },
            LlmResponseId = "resp-1",
            Result = new ToolResult { ToolCallId = "call_weather_123", Content = "Sunny, 72°F" },
        };

        var back = EventJson.RoundTrip(e);

        Assert.Equal(e.Result, back.Result);
        Assert.Equal("resp-1", back.LlmResponseId);
    }
}
