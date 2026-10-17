// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_renderer.py, tests/context_blocks/test_truncation.py

using System.Text.Json.Nodes;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Llm;

namespace Nooa.Tests.ContextBlocks;

public class ContextRendererTests
{
    private static readonly BlockFormatter _xml = new XmlBlockFormatter();

    private static ResolvedBlock Event(EventBase evt, string tag = "1") =>
        new("event_" + tag, "", evt.EventRole, new BlockMetadata(Tag: tag), evt);

    private static string Content(LlmMessage message) => ((JsonMessage)message).Value["content"]!.GetValue<string>();

    [Fact]
    public void Render_EmptyBlocks_GivesNoMessagesAndZeroStats()
    {
        var result = ContextRenderer.Render([], _xml);

        Assert.Empty(result.Messages);
        Assert.Equal((0, 0), (result.Stats.ContextBlocksCount, result.Stats.EventsCount));
    }

    [Fact]
    public void Render_SystemBlocks_CountCharactersInStats()
    {
        var result = ContextRenderer.Render([new ResolvedBlock("a", "hello"), new ResolvedBlock("b", "xy")], _xml);

        Assert.Equal(2, result.Stats.ContextBlocksCount);
        Assert.Equal(7, result.Stats.ContextBlocksChars);
        Assert.Null(result.Stats.PromptTokens);
    }

    [Fact]
    public void Render_EventBlock_IsSerializedAtRenderTime()
    {
        var result = ContextRenderer.Render([Event(new TaskEvent { Prompt = "Do it" })], _xml);

        Assert.Equal("<sys tag=\"1\">\nTask { Prompt = \"Do it\" }\n</sys>", Content(result.Messages[1]));
        Assert.Equal(1, result.Stats.EventsCount);
        Assert.Equal("Task { Prompt = \"Do it\" }".Length, result.Stats.EventsChars);
    }

    [Fact]
    public void Render_DoesNotChangeTheInputBlocks()
    {
        var blocks = new List<ResolvedBlock> { new("a", "x"), Event(new TaskEvent { Prompt = "p" }) };

        var first = ContextRenderer.Render(blocks, _xml);
        var second = ContextRenderer.Render(blocks, _xml);

        Assert.Equal("", blocks[1].Content);
        Assert.Equal(first.Messages.Select(Content), second.Messages.Select(Content));
    }

    [Fact]
    public void Render_ToolCallEvent_IsNotSerializedAsText()
    {
        var call = new ToolCallEvent { ToolCallId = "c1", Name = "t", Arguments = new JsonObject(), Result = new ToolResult { ToolCallId = "c1", Content = "ok" } };

        var result = ContextRenderer.Render([Event(call)], _xml);

        Assert.Equal(0, result.Stats.EventsChars);
        Assert.Equal("ok", Content(result.Messages[^1]));
    }

    [Fact]
    public void Render_EventFormat_BoundsTheEventFields()
    {
        var result = ContextRenderer.Render(
            [Event(new Error { Content = new string('x', 400) })], _xml, eventFormat: new FormatConfig { MaxString = 40 });

        Assert.Contains("string(len=400", Content(result.Messages[1]));
    }

    [Fact]
    public void Render_EventFormatResolver_OverridesTheDefaultForOneEvent()
    {
        var long1 = new Error { Content = new string('a', 400) };
        var long2 = new Error { Content = new string('b', 400) };

        var result = ContextRenderer.Render(
            [Event(long1, "1"), Event(long2, "2")],
            _xml,
            eventFormat: new FormatConfig { MaxString = 40 },
            eventFormatResolver: e => e == long2 ? new FormatConfig { MaxString = null } : null);

        Assert.Contains("string(len=400", Content(result.Messages[1]));
        Assert.Contains(new string('b', 400), Content(result.Messages[2]));
    }

    [Fact]
    public void Render_Markdown_UsesHeaders()
    {
        var result = ContextRenderer.Render([new ResolvedBlock("notes", "hello")], new MarkdownBlockFormatter());

        Assert.Equal("# Notes\n\nhello", Content(result.Messages[0]));
    }

    [Fact]
    public void Render_LongBlockWithoutLimit_PassesThroughUnchanged()
    {
        var text = new string('z', 50_000);

        var result = ContextRenderer.Render([new ResolvedBlock("big", text)], _xml);

        Assert.Contains(text, Content(result.Messages[0]));
    }

    [Fact]
    public void Render_LimitWithoutCounter_Throws() =>
        Assert.Throws<ArgumentException>(() => ContextRenderer.Render([], _xml, contextLimit: 10));

    [Fact]
    public void Render_NoLimit_NeedsNoCounter() => ContextRenderer.Render([new ResolvedBlock("a", "x")], _xml);

    [Fact]
    public void Render_OverBudget_EvictsUserBlocksFirstFromTheEnd()
    {
        var blocks = new[]
        {
            new ResolvedBlock("framework", new string('f', 100)),
            new ResolvedBlock("user1", new string('1', 100), Metadata: new BlockMetadata(UserBlock: true)),
            new ResolvedBlock("user2", new string('2', 100), Metadata: new BlockMetadata(UserBlock: true)),
        };

        var result = ContextRenderer.Render(blocks, _xml, contextLimit: 200, countTokens: t => t.Length);

        var text = Content(result.Messages[0]);
        Assert.Contains("<user2>\nEVICTED: over context budget (block_tokens=100)\n</user2>", text);
        Assert.Contains(new string('1', 100), text);
        Assert.Contains(new string('f', 100), text);
        Assert.Equal(1, result.Stats.ContextBlocksDropped);
    }

    [Fact]
    public void Render_StillOverBudget_EvictsOtherBlocksButNeverStaticOnes()
    {
        var blocks = new[]
        {
            new ResolvedBlock("rules", new string('r', 100), Metadata: new BlockMetadata(Static: true)),
            new ResolvedBlock("state", new string('s', 100)),
            new ResolvedBlock("notes", new string('n', 100), Metadata: new BlockMetadata(UserBlock: true)),
        };

        var result = ContextRenderer.Render(blocks, _xml, contextLimit: 100, countTokens: t => t.Length);

        var text = Content(result.Messages[0]);
        Assert.Contains(new string('r', 100), text);
        Assert.DoesNotContain(new string('s', 100), text);
        Assert.DoesNotContain(new string('n', 100), text);
        Assert.Equal(2, result.Stats.ContextBlocksDropped);
    }

    [Fact]
    public void Render_UnderBudget_EvictsNothing()
    {
        var result = ContextRenderer.Render([new ResolvedBlock("a", "short")], _xml, contextLimit: 100, countTokens: t => t.Length);

        Assert.Equal(0, result.Stats.ContextBlocksDropped);
        Assert.Equal(100, result.Stats.MaxContextTokens);
    }

    [Fact]
    public void Render_UsesTheCounterForTheBudget()
    {
        var blocks = new[] { new ResolvedBlock("a", "aaaa"), new ResolvedBlock("b", "bbbb", Metadata: new BlockMetadata(UserBlock: true)) };

        var result = ContextRenderer.Render(blocks, _xml, contextLimit: 3, countTokens: t => t.Length / 4);

        Assert.Equal(0, result.Stats.ContextBlocksDropped);
        Assert.Equal(1, ContextRenderer.Render(blocks, _xml, contextLimit: 1, countTokens: t => t.Length / 4).Stats.ContextBlocksDropped);
    }

    [Fact]
    public void Render_PassesWindowAndReserveToTheStats()
    {
        var result = ContextRenderer.Render([], _xml, modelContextWindow: 1000, reservedOutputTokens: 100);

        Assert.Equal((1000, 100), (result.Stats.ModelContextWindow, result.Stats.ReservedOutputTokens));
    }

    [Fact]
    public void Render_NullBlocks_Throws() => Assert.Throws<ArgumentNullException>(() => ContextRenderer.Render(null!, _xml));
}
