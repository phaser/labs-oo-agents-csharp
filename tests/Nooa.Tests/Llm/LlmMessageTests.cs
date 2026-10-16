// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class LlmMessageTests
{
    [Fact]
    public void User_Content_BuildsOpenAiChatMessage() =>
        Assert.Equal(
            """{"role":"user","content":"hi"}""",
            ((JsonMessage)LlmMessage.User("hi")).Value.ToJsonString());

    [Fact]
    public void Tool_IdAndContent_BuildsToolMessage() =>
        Assert.Equal(
            """{"role":"tool","tool_call_id":"c1","content":"ok"}""",
            ((JsonMessage)LlmMessage.Tool("c1", "ok")).Value.ToJsonString());
}
