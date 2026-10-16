// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class AssistantMessageTests
{
    [Fact]
    public void Build_TextOnly_HasRoleAndContent()
    {
        var message = AssistantMessage.Build(null, []);

        Assert.Equal("""{"role":"assistant","content":""}""", message.ToJsonString());
    }

    [Fact]
    public void Build_WithReasoningAndCalls_KeepsArgumentsIntact()
    {
        var call = new ToolCall { Id = "c1", Name = "run", Arguments = "{ \"a\":  1 }" };

        var message = AssistantMessage.Build("hi", [call], "why");

        Assert.Equal("why", (string?)message["reasoning_content"]);
        var function = message["tool_calls"]![0]!["function"]!;
        Assert.Equal("{ \"a\":  1 }", (string?)function["arguments"]);
        Assert.Equal("function", (string?)message["tool_calls"]![0]!["type"]);
    }
}
