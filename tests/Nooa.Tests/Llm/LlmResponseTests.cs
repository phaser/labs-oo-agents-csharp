// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_llm_response_event.py, tests/unifiedllm/test_replay_copy_ownership.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;
using Nooa.Json;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class LlmResponseTests
{
    private static LlmResponse Sample() => new()
    {
        RawResponse = new object(),
        Parsed = new { value = 42 },
        Parts =
        [
            new AssistantText { Text = "{\"value\":42}" },
            new AssistantReasoning { Text = "plain reasoning", Native = new NativeJson(new JsonObject { ["opaque"] = "provider-state" }) },
        ],
        ReplayScope = "responses:openai:test",
        Usage = new LlmUsage { InputTokens = 12, CachedInputTokens = 8 },
        ModelName = "m",
        GenerationId = "g",
        DynamicContext = "ctx",
    };

    [Fact]
    public void Serialize_ExcludesEphemeralViews()
    {
        var json = EventJson.Write(Sample());

        Assert.DoesNotContain("raw_response", json);
        Assert.DoesNotContain("parsed", json);
        Assert.Contains("\"cached_input_tokens\":8", json);
    }

    [Fact]
    public void RoundTrip_KeepsDurableFields()
    {
        var back = EventJson.RoundTrip(Sample());

        Assert.Null(back.RawResponse);
        Assert.Null(back.Parsed);
        Assert.Equal("responses:openai:test", back.ReplayScope);
        Assert.Equal(8, back.Usage!.CachedInputTokens);
        Assert.Equal(("m", "g", "ctx"), (back.ModelName, back.GenerationId, back.DynamicContext));
        Assert.Equal("plain reasoning", back.Reasoning);
        Assert.Equal("provider-state", (string?)back.Parts[1].Native!.ToJsonObject()["opaque"]);
    }

    [Fact]
    public void Defaults_AreAssistantRoleStopAndEmptyParts()
    {
        var r = new LlmResponse();

        Assert.Equal(Role.Assistant, r.EventRole);
        Assert.Equal("LLMResponse", r.EventType);
        Assert.Equal(FinishReason.Stop, r.FinishReason);
        Assert.Empty(r.Parts);
        Assert.Equal("", r.Content);
        Assert.Null(r.Reasoning);
        Assert.Null(r.Usage);
    }

    [Fact]
    public void Projections_JoinTextAndReasoning_InPartOrder()
    {
        var r = new LlmResponse
        {
            Parts =
            [
                new AssistantText { Text = "a" },
                new AssistantReasoning { Text = "r1" },
                new AssistantReasoning { Text = "" },
                new AssistantText { Text = "b" },
                new AssistantReasoning { Text = "r2" },
                new ToolCall { Id = "1", Name = "n", Arguments = "{}" },
            ],
        };

        Assert.Equal("ab", r.Content);
        Assert.Equal("r1\nr2", r.Reasoning);
        Assert.Single(r.ToolCalls);
    }

    [Theory]
    [InlineData(FinishReason.ToolCalls, "{\"a\":1}", true)]
    [InlineData(FinishReason.Stop, "{\"a\":1}", false)]
    [InlineData(FinishReason.ToolCalls, "{\"a\":", false)]
    [InlineData(FinishReason.ToolCalls, "[1]", false)]
    [InlineData(FinishReason.ToolCalls, "", false)]
    public void ReplayToolCalls_OnlyCompleteObjectCalls(FinishReason reason, string arguments, bool replayable)
    {
        var r = new LlmResponse
        {
            FinishReason = reason,
            Parts = [new ToolCall { Id = "1", Name = "n", Arguments = arguments }],
        };

        Assert.Equal(replayable, r.ReplayToolCalls.Count == 1);
    }

    [Fact]
    public void IsEmpty_BlankTextWithoutOtherContent_IsTrue()
    {
        Assert.True(new LlmResponse().IsEmpty);
        Assert.True(new LlmResponse { Parts = [new AssistantText { Text = "  \n" }] }.IsEmpty);
    }

    [Fact]
    public void IsEmpty_NativeStateOnly_IsFalse()
    {
        var native = new NativeJson(new JsonObject { ["encrypted_content"] = "x" });

        Assert.False(new LlmResponse { Parts = [new AssistantReasoning { Native = native }] }.IsEmpty);
        Assert.False(new LlmResponse { Parts = [new AssistantText { Text = "x" }] }.IsEmpty);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("parts")]
    public void Replace_EditedResponseOwnsItsMetadata(string edit)
    {
        var original = new LlmResponse { Parts = [new AssistantText { Text = "before" }], Metadata = new JsonObject { ["k"] = 1 } };

        var edited = edit == "text" ? original.ReplaceText("after") : original.ReplaceParts([new AssistantText { Text = "after" }]);
        edited.Metadata["k"] = 2;

        Assert.Equal(1, (int?)original.Metadata["k"]);
        Assert.Equal("after", edited.Content);
    }

    [Fact]
    public void ReplaceParts_DropsNativeScopeRawAndParsed()
    {
        var original = Sample();

        var edited = original.ReplaceParts(original.Parts);

        Assert.All(edited.Parts, p => Assert.Null(p.Native));
        Assert.Null(edited.ReplayScope);
        Assert.Null(edited.RawResponse);
        Assert.Null(edited.Parsed);
        Assert.Equal("m", edited.ModelName);
        Assert.NotNull(original.Parts[1].Native);
        Assert.Equal("responses:openai:test", original.ReplayScope);
        Assert.Equal(original.Id, edited.Id);
    }

    [Fact]
    public void ReplaceText_KeepsReasoningAndToolCalls_PutsTextFirst()
    {
        var original = new LlmResponse
        {
            Parts = [new AssistantReasoning { Text = "why" }, new AssistantText { Text = "old" }, new ToolCall { Id = "1", Name = "n", Arguments = "{}" }],
        };

        var edited = original.ReplaceText("new");

        Assert.IsType<AssistantText>(edited.Parts[0]);
        Assert.Equal("new", edited.Content);
        Assert.Equal("why", edited.Reasoning);
        Assert.Single(edited.ToolCalls);
    }

    [Fact]
    public void PublicMessage_ExcludesNativeState()
    {
        var message = Sample().PublicMessage().ToJsonString();

        Assert.DoesNotContain("provider-state", message);
        Assert.Contains("plain reasoning", message);
    }
}
