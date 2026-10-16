// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_llm_response_event.py

using System.Text.Json.Nodes;
using Nooa.Json;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class AssistantPartTests
{
    [Fact]
    public void Serialize_UsesKindDiscriminator()
    {
        var response = new LlmResponse
        {
            Parts = [new AssistantText { Text = "t" }, new ToolCall { Id = "i", Name = "n", Arguments = "{}" }, new AssistantReasoning { Text = "r" }],
        };

        var json = EventJson.Write(response);

        Assert.Contains("\"kind\":\"text\"", json);
        Assert.Contains("\"kind\":\"tool_call\"", json);
        Assert.Contains("\"kind\":\"reasoning\"", json);
    }

    [Fact]
    public void RoundTrip_KeepsOrderAndNative()
    {
        var native = new NativeJson(new JsonObject { ["encrypted_content"] = "blob" });
        var response = new LlmResponse
        {
            Parts = [new AssistantReasoning { Native = native }, new AssistantText { Text = "a" }],
        };

        var back = EventJson.RoundTrip(response);

        Assert.IsType<AssistantReasoning>(back.Parts[0]);
        Assert.Equal(native, back.Parts[0].Native);
        Assert.IsType<AssistantText>(back.Parts[1]);
    }

    [Fact]
    public void Deserialize_UnknownKind_Throws()
    {
        Assert.ThrowsAny<Exception>(() => EventJson.Read("""{"event_type":"LLMResponse","parts":[{"kind":"evil","text":"x"}]}"""));
    }
}
