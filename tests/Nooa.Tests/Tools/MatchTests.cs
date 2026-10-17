// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_tools_modern_behavior.py (Match tests)

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class MatchTests
{
    private static readonly string _file = Path.Combine(Path.GetTempPath(), "f.txt");

    [Fact]
    public void Constructor_RelativeResolvedPath_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => new Match("f.txt", 1, 1, "hello\n", "f.txt"));

        Assert.Contains("absolute", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_AbsoluteResolvedPath_IsKept()
    {
        var match = new Match("f.txt", 1, 1, "hello\n", _file);

        Assert.Equal(Path.GetFullPath(_file), match.ResolvedPath);
    }

    [Fact]
    public void Numbered_PadsLineNumbersToTheWidthOfTheLastLine()
    {
        var match = new Match("f.txt", 9, 10, "a\nb\n", _file);

        Assert.Equal(" 9| a\n10| b", match.Numbered);
        Assert.Equal(match.Numbered, match.ToString());
    }

    [Fact]
    public void Slice_NarrowsTheRegion()
    {
        var match = new Match("f.txt", 5, 8, "a\nb\nc\nd\n", _file);

        var narrowed = match.Slice(6, 7);

        Assert.Equal((6, 7, "b\nc\n"), (narrowed.Start, narrowed.End, narrowed.Text));
        Assert.Equal(_file, narrowed.ResolvedPath, ignoreCase: false);
    }

    [Fact]
    public void Slice_NullBounds_KeepTheCurrentBounds()
    {
        var match = new Match("f.txt", 5, 8, "a\nb\nc\nd\n", _file);

        Assert.Equal("b\nc\nd\n", match.Slice(start: 6).Text);
        Assert.Equal("a\nb\n", match.Slice(end: 6).Text);
    }

    [Fact]
    public void Slice_BoundsOutsideTheRegion_AreClamped()
    {
        var match = new Match("f.txt", 5, 6, "a\nb\n", _file);

        var narrowed = match.Slice(1, 99);

        Assert.Equal((5, 6, "a\nb\n"), (narrowed.Start, narrowed.End, narrowed.Text));
    }

    [Fact]
    public void Slice_EmptyRange_GivesEmptyText()
    {
        var match = new Match("f.txt", 1, 1, "captured\n", _file);

        Assert.Equal(string.Empty, match.Slice(1, 0).Text);
        Assert.Equal(string.Empty, match.Slice(5, 8).Text);
    }

    [Fact]
    public void Describe_ShowsPathAndRange() => Assert.Equal("Match(\"f.txt\", lines 2-3)", new Match("f.txt", 2, 3, "a\nb\n", _file).Describe());
}
