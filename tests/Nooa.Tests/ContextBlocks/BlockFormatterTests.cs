// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_formatters.py, tests/context_blocks/test_format_event_bounded.py

using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Llm;

namespace Nooa.Tests.ContextBlocks;

public class BlockFormatterTests
{
    private readonly BlockFormatter _formatter = new XmlBlockFormatter();

    [Fact]
    public void FormatEvent_UserEvent_ShowsTheNameAndFields() =>
        Assert.Equal("UserEvent { Content = \"hello\" }", _formatter.FormatEvent(new UserEvent { Content = "hello" }));

    [Fact]
    public void FormatEvent_TaskEvent_ShowsTheNameTask() =>
        Assert.StartsWith("Task { ", _formatter.FormatEvent(new TaskEvent { Prompt = "p" }));

    [Fact]
    public void FormatEvent_AssistantEventWithText_ReplaysVerbatim() =>
        Assert.Equal("plain\ntext", _formatter.FormatEvent(new Message { Content = "plain\ntext" }));

    [Fact]
    public void FormatEvent_Response_ReplaysTheVisibleText()
    {
        var response = new LlmResponse { Parts = [new AssistantText { Text = "answer" }, new AssistantReasoning { Text = "hidden thoughts" }] };

        Assert.Equal("answer", _formatter.FormatEvent(response));
    }

    [Fact]
    public void FormatEvent_SmallEvent_IsUnchangedByTheBounds() =>
        Assert.Equal("Error { Content = \"boom\" }", _formatter.FormatEvent(new Error { Content = "boom" }, new FormatConfig { MaxString = 100, MaxLength = 5, MaxDepth = 2 }));

    [Fact]
    public void FormatEvent_LongString_UsesAMarker()
    {
        var text = _formatter.FormatEvent(new Error { Content = new string('x', 300) }, new FormatConfig { MaxString = 50 });

        Assert.Contains("string(len=300", text);
        Assert.True(text.Length < 200);
    }

    [Fact]
    public void FormatEvent_NullEvent_Throws() => Assert.Throws<ArgumentNullException>(() => _formatter.FormatEvent(null!));

    [Fact]
    public void FormatDescription_Default_IsEmpty() => Assert.Equal("", new EmptyFormatter().FormatDescription());

    private sealed class EmptyFormatter : BlockFormatter
    {
        public override IReadOnlyList<LlmMessage> Format(IReadOnlyList<ResolvedBlock> blocks, Microsoft.Extensions.Logging.ILogger? logger = null) => [];
    }
}
