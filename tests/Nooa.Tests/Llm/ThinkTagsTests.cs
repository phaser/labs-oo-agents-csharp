// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_reasoning_completion_identity.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ThinkTagsTests
{
    [Fact]
    public void Extract_CompleteBlock_SplitsReasoningAndAnswer()
    {
        var (content, reasoning) = ThinkTags.Extract("<think>\nlet me see\n</think>\nThe answer is 4.");

        Assert.Equal("The answer is 4.", content);
        Assert.Equal("let me see", reasoning);
    }

    [Fact]
    public void Extract_MissingOpeningTag_TreatsTextBeforeClosingTagAsReasoning()
    {
        var (content, reasoning) = ThinkTags.Extract("hmm, thinking</think>Done.");

        Assert.Equal("Done.", content);
        Assert.Equal("hmm, thinking", reasoning);
    }

    [Fact]
    public void Extract_NoTag_ReturnsContentUnchanged()
    {
        var (content, reasoning) = ThinkTags.Extract("  plain  ");

        Assert.Equal("  plain  ", content);
        Assert.Null(reasoning);
    }

    [Fact]
    public void Extract_BlockOnly_LeavesEmptyContent()
    {
        var (content, reasoning) = ThinkTags.Extract("<think>only thoughts</think>");

        Assert.Equal("", content);
        Assert.Equal("only thoughts", reasoning);
    }

    [Fact]
    public void Extract_ManyUnclosedOpeningTags_ReturnsInTime()
    {
        var text = string.Concat(Enumerable.Repeat("<think>", 50_000));

        var (content, reasoning) = ThinkTags.Extract(text);

        Assert.Equal(text, content);
        Assert.Null(reasoning);
    }
}
