// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_formatters.py

using Nooa.ContextBlocks;
using Nooa.Llm;

namespace Nooa.Tests.ContextBlocks;

public class MarkdownBlockFormatterTests
{
    private static readonly MarkdownBlockFormatter _formatter = new();

    private static string SystemText(IReadOnlyList<LlmMessage> messages) => ((JsonMessage)messages[0]).Value["content"]!.GetValue<string>();

    [Fact]
    public void Format_Empty_GivesNoMessages() => Assert.Empty(_formatter.Format([]));

    [Fact]
    public void Format_SingleBlock_UsesAHeader() =>
        Assert.Equal("# Notes\n\nhello", SystemText(_formatter.Format([new ResolvedBlock("notes", "hello")])));

    [Fact]
    public void Format_MultipleBlocks_AreSeparatedByABlankLine() =>
        Assert.Equal("# A\n\n1\n\n# B\n\n2", SystemText(_formatter.Format([new ResolvedBlock("a", "1"), new ResolvedBlock("b", "2")])));

    [Fact]
    public void Format_KeyWithUnderscores_BecomesTitleCaseWords() =>
        Assert.Equal("# System Prompt\n\nx", SystemText(_formatter.Format([new ResolvedBlock("system_prompt", "x")])));

    [Fact]
    public void FormatDescription_NamesTheHeaders() => Assert.Contains("# Block Name", _formatter.FormatDescription());
}
