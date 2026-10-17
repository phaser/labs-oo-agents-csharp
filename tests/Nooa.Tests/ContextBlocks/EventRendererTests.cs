// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_events.py, tests/context_blocks/test_format_event_bounded.py, tests/test_events.py (rendering)

using System.Text.Json.Nodes;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Llm;

namespace Nooa.Tests.ContextBlocks;

public class EventRendererTests
{
    [Fact]
    public void Format_TaskEvent_ShowsTheNameTask()
    {
        var text = EventRenderer.Format(new TaskEvent { Prompt = "Do it" });

        Assert.Equal("Task { Prompt = \"Do it\" }", text);
    }

    [Fact]
    public void Format_HidesFrameworkFields()
    {
        var evt = new UserEvent { Content = "hi", Tag = "3", Metadata = new JsonObject { ["call_id"] = "c" } };

        var text = EventRenderer.Format(evt);

        Assert.Equal("UserEvent { Content = \"hi\" }", text);
        Assert.DoesNotContain(evt.Id, text);
        Assert.DoesNotContain("call_id", text);
        Assert.DoesNotContain("Timestamp", text);
    }

    [Fact]
    public void Format_HidesFieldsMarkedNotShownToTheModel()
    {
        var evt = new CellOutput { ToolCallId = "c1", ExecutionStatus = ResultStatus.Complete, ExecutionCount = 7, Stdout = "out", ExplicitReturn = true };

        var text = EventRenderer.Format(evt);

        Assert.Contains("Stdout = \"out\"", text);
        Assert.DoesNotContain("ExecutionCount", text);
        Assert.DoesNotContain("ExplicitReturn", text);
        Assert.DoesNotContain("Images", text);
    }

    [Fact]
    public void Format_LoopGuard_ShowsOnlyTheMessage()
    {
        var evt = new LoopGuardTriggered { Content = "stop repeating", Action = "blocked", ToolName = "t", Repeats = 3, Window = 5, Fingerprint = "abc" };

        Assert.Equal("LoopGuardTriggered { Content = \"stop repeating\" }", EventRenderer.Format(evt));
    }

    [Fact]
    public void Format_EmptyValues_AreSuppressed_ButZeroAndFalseStay()
    {
        var evt = new LlmCallEnd { MethodName = "m", Strategy = "", GenerationId = "g", TurnNumber = 0, Success = false };

        var text = EventRenderer.Format(evt);

        Assert.Contains("TurnNumber = 0", text);
        Assert.Contains("Success = false", text);
        Assert.DoesNotContain("Strategy", text);
        Assert.DoesNotContain("ExceptionType", text);
    }

    [Fact]
    public void Format_EmptyCollectionAndNullValue_AreSuppressed()
    {
        var evt = new Summary { SummaryTag = "1..3", ReplacedRange = new SeqRange(1, 3), SummaryText = null };

        var text = EventRenderer.Format(evt);

        Assert.DoesNotContain("SummaryText", text);
        Assert.DoesNotContain("ChildrenTags", text);
        Assert.Contains("SummaryTag = \"1..3\"", text);
    }

    [Fact]
    public void Format_LongTaskPrompt_IsNotTruncated()
    {
        var prompt = new string('p', 30_000);

        var text = EventRenderer.Format(new TaskEvent { Prompt = prompt }, new FormatConfig { MaxString = 100 });

        Assert.Contains(prompt, text);
        Assert.DoesNotContain("len=", text);
    }

    [Fact]
    public void Format_LongStdoutAndStderr_AreNotTruncated()
    {
        var big = new string('o', 20_000);
        var evt = new CellOutput { ToolCallId = "c", ExecutionStatus = ResultStatus.Complete, ExecutionCount = 1, Stdout = big, Stderr = big };

        var text = EventRenderer.Format(evt, new FormatConfig { MaxString = 100 });

        Assert.Equal(2, text.Split(big).Length - 1);
    }

    [Fact]
    public void Format_OtherStringField_UsesTheConfiguredLimit()
    {
        var evt = new CellOutput { ToolCallId = "c", ExecutionStatus = ResultStatus.Error, ExecutionCount = 1, Error = new string('e', 500) };

        var text = EventRenderer.Format(evt, new FormatConfig { MaxString = 50 });

        Assert.Contains("string(len=500", text);
    }

    [Fact]
    public void Format_Notification_CapsDescriptionAt20000()
    {
        var text = EventRenderer.Format(
            new Notification { Source = "queue:x", Description = new string('d', 25_000) },
            new FormatConfig { MaxString = 100 });

        Assert.Contains("string(len=25000", text);
        Assert.True(text.Length is > 19_000 and < 21_000);
    }

    [Fact]
    public void Format_Notification_ShortDescriptionIsNotCutByTheSmallDefault()
    {
        var description = new string('d', 3_000);

        var text = EventRenderer.Format(new Notification { Source = "s", Description = description }, new FormatConfig { MaxString = 100 });

        Assert.Contains(description, text);
    }

    [Fact]
    public void Format_NullConfig_UsesTheFormatConfigDefaults()
    {
        var evt = new Error { Content = new string('x', 600) };

        Assert.Contains("string(len=600", EventRenderer.Format(evt));
    }

    [Fact]
    public void Format_JsonValue_PrintsAsPlainValues()
    {
        var evt = new CellOutput
        {
            ToolCallId = "c",
            ExecutionStatus = ResultStatus.Complete,
            ExecutionCount = 1,
            Value = new JsonObject { ["n"] = 5, ["items"] = new JsonArray("a", "b"), ["flag"] = true, ["nothing"] = null },
        };

        var text = EventRenderer.Format(evt);

        Assert.Contains("Value = {\"n\": 5, \"items\": [\"a\", \"b\"], \"flag\": true, \"nothing\": null}", text);
    }

    [Fact]
    public void Format_EventBounds_LimitLargeCollections()
    {
        var evt = new CellOutput
        {
            ToolCallId = "c",
            ExecutionStatus = ResultStatus.Complete,
            ExecutionCount = 1,
            Value = new JsonArray(Enumerable.Range(0, 100).Select(i => (JsonNode?)i).ToArray()),
        };

        var text = EventRenderer.Format(evt, new FormatConfig { MaxLength = 6 });

        Assert.Contains("len=100", text);
    }

    [Fact]
    public void SearchableFields_AreThePublicValues()
    {
        var fields = EventRenderer.SearchableFields(new TaskEvent { Prompt = "find me", Tag = "1" });

        Assert.Equal(["Prompt"], fields.Keys);
        Assert.Equal("find me", fields["Prompt"]);
    }

    [Fact]
    public void SearchableFields_Response_ShowsReadableValuesNotParts()
    {
        var response = new LlmResponse
        {
            Parts =
            [
                new AssistantText { Text = "answer" },
                new AssistantReasoning { Text = "because" },
                new ToolCall { Id = "c1", Name = "lookup", Arguments = "{}" },
            ],
            FinishReason = FinishReason.ToolCalls,
        };

        var fields = EventRenderer.SearchableFields(response);

        Assert.Equal("answer", fields["Content"]);
        Assert.Equal("because", fields["Reasoning"]);
        Assert.Single((IReadOnlyList<ToolCall>)fields["ToolCalls"]!);
        Assert.DoesNotContain("Parts", fields.Keys);
        Assert.DoesNotContain("Usage", fields.Keys);
    }

    [Fact]
    public void SearchableText_NoFields_IsTheEventType() =>
        Assert.Equal("LLMResponse", EventRenderer.SearchableText(new LlmResponse()));

    [Fact]
    public void StringContent_ReadsAStringContentProperty()
    {
        Assert.Equal("hi", EventRenderer.StringContent(new Message { Content = "hi" }));
        Assert.Null(EventRenderer.StringContent(new TaskEvent { Prompt = "p" }));
    }
}
