// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class UnifiedDiffTests
{
    private static string Diff(string expected, string current)
        => UnifiedDiff.Build(TextLines.Split(expected), TextLines.Split(current), "a", "b");

    [Fact]
    public void Build_OneLineChange_MatchesDifflib()
    {
        Assert.Equal("--- a\n+++ b\n@@ -1 +1 @@\n-old\n+new\n", Diff("old\n", "new\n"));
    }

    [Fact]
    public void Build_EqualInputs_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Diff("x\ny\n", "x\ny\n"));
    }

    [Fact]
    public void Build_ChangeInTheMiddle_UsesThreeLinesOfContext()
    {
        var expected = string.Concat(Enumerable.Range(1, 20).Select(i => $"l{i}\n"));
        var current = expected.Replace("l10\n", "ten\n", StringComparison.Ordinal);

        Assert.Equal(
            "--- a\n+++ b\n@@ -7,7 +7,7 @@\n l7\n l8\n l9\n-l10\n+ten\n l11\n l12\n l13\n",
            Diff(expected, current));
    }

    [Fact]
    public void Build_FarApartChanges_MakeTwoHunks()
    {
        var expected = string.Concat(Enumerable.Range(1, 30).Select(i => $"l{i}\n"));
        var current = expected.Replace("l3\n", "x\n", StringComparison.Ordinal).Replace("l28\n", "y\n", StringComparison.Ordinal);

        var diff = Diff(expected, current);

        Assert.Equal(2, diff.Split('\n').Count(line => line.StartsWith("@@", StringComparison.Ordinal)));
    }

    [Fact]
    public void Build_InsertionAndDeletion_UseEmptyRanges()
    {
        Assert.Equal("--- a\n+++ b\n@@ -0,0 +1 @@\n+a\n", Diff(string.Empty, "a\n"));
        Assert.Equal("--- a\n+++ b\n@@ -1 +0,0 @@\n-a\n", Diff("a\n", string.Empty));
    }

    [Fact]
    public void Build_MissingFinalNewline_AddsNote()
    {
        var diff = Diff("old", "new");

        Assert.Equal(2, diff.Split("No newline at end of file").Length - 1);
    }
}
