// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_tools_stale_match.py

using System.Text;
using Nooa.Tools;

namespace Nooa.Tests.Tools;

public sealed class StaleMatchExceptionTests : IAsyncLifetime
{
    private readonly ShellTestDirectory _directory = new();
    private readonly ShellTools _shell;

    public StaleMatchExceptionTests()
    {
        _shell = new ShellTools(_directory.Options());
    }

    public static TheoryData<string> StaleContents =>
    [
        "first\nmodified\nthird\n",
        "inserted\nfirst\ncaptured\nthird\n", // The captured text still exists: no relocation.
        "first\nthird\n",
        "first\n",
        "",
        "first\ncaptured", // Only the final newline differs.
    ];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _shell.DisposeAsync();
        _directory.Dispose();
    }

    private string PathOf(string name) => _directory.File(name);

    [Theory]
    [MemberData(nameof(StaleContents))]
    public async Task ReplaceAsync_StaleRegion_NeverWrites(string current)
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "first\ncaptured\nthird\n", TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", (2, 2), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, current, TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal(current, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(StaleMatchException.ErrorCode, "STALE_MATCH");
        Assert.Equal("file.txt", error.Path);
        Assert.Equal(path, error.ResolvedPath);
        Assert.Equal((2, 2), (error.Start, error.End));
        foreach (var text in new[] { "file.txt", path, "captured lines 2-2", "No file was changed", "Re-read or re-search", "recompute the Match", "never relocated" })
        {
            Assert.Contains(text, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ReplaceAsync_MissingFile_IsNeverCreated()
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "captured\n", TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);
        File.Delete(path);

        var error = await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
        Assert.Contains("file no longer exists", error.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.Diff);
        Assert.False(error.DiffComplete);
        Assert.Contains("unavailable", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Diff preview truncated", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("new first\ncaptured\nthird\n")]
    [InlineData("first\ncaptured\nnew last\nextra\n")]
    public async Task ReplaceAsync_UnrelatedRegionChanges_AreAllowed(string current)
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "first\ncaptured\nthird\n", TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", (2, 2), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, current, TestContext.Current.CancellationToken);

        var result = await _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken);

        Assert.Equal(current.Replace("captured\n", "replacement\n", StringComparison.Ordinal), await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal("replacement\n", result.NewText);
    }

    [Theory]
    [InlineData("whole", "\n")]
    [InlineData("whole", "\r\n")]
    [InlineData("whole", "\r")]
    [InlineData("whole", "")]
    [InlineData("window", "\n")]
    [InlineData("window", "\r\n")]
    [InlineData("slice", "\n")]
    [InlineData("slice", "")]
    public async Task ReplaceAsync_ValidProducers_NormalizeLineEndingsLikeUpstream(string producer, string ending)
    {
        var path = PathOf("file.txt");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("captured" + ending), TestContext.Current.CancellationToken);
        var anchor = producer switch
        {
            "whole" => await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken),
            "window" => await _shell.ReadAsync("file.txt", (1, 1), TestContext.Current.CancellationToken),
            _ => (await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken)).Slice(1, 1),
        };

        var result = await _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken);

        var expected = "replacement" + (ending.Length > 0 ? "\n" : string.Empty);
        Assert.Equal("captured" + (ending.Length > 0 ? "\n" : string.Empty), anchor.Text);
        Assert.Equal(expected, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(expected, result.NewText);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 5)]
    [InlineData(5, 5)]
    public async Task ReplaceAsync_InvalidOrOutOfBoundsRange_NeverWrites(int start, int end)
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "captured\n", TestContext.Current.CancellationToken);
        var anchor = new Match("file.txt", start, end, "captured\n", path);

        await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal("captured\n", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceAsync_InvalidReadAndSliceRanges_NeverWrite()
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "captured\n", TestContext.Current.CancellationToken);
        var whole = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);

        foreach (var anchor in new[] { await _shell.ReadAsync("file.txt", (5, 8), TestContext.Current.CancellationToken), whole.Slice(5, 8), whole.Slice(1, 0) })
        {
            await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));
            Assert.Equal("captured\n", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task ReplaceAsync_EmptyFileAnchor_IsValidButDetectsAddedContent()
    {
        var path = PathOf("file.txt");
        await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);

        await _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken);

        Assert.Equal((1, 0, string.Empty), (anchor.Start, anchor.End, anchor.Text));
        Assert.Equal("replacement", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "overwrite", TestContext.Current.CancellationToken));
        Assert.Equal("replacement", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceAsync_SmallDiff_IsComplete()
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "old\n", TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, "new\n", TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal("--- stored Match.text (when read)\n+++ current file (same saved line range)\n@@ -1 +1 @@\n-old\n+new\n", error.Diff);
        Assert.True(error.DiffComplete);
        Assert.EndsWith(error.Diff, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Diff preview truncated", error.Message, StringComparison.Ordinal);
        Assert.Contains("stored Match.text (when read) versus current file (same saved line range) diff (complete):", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("line-cap", "capped at 20 lines and 2 KiB")]
    [InlineData("byte-cap", "capped at 20 lines and 2 KiB")]
    [InlineData("huge-line", "exceed 16 KiB")]
    [InlineData("utf8-input-cap", "exceed 16 KiB")]
    [InlineData("line-input-cap", "exceed 400 lines")]
    public async Task ReplaceAsync_LargeDiff_IsBoundedOrOmitted(string kind, string detail)
    {
        var (expected, current) = kind switch
        {
            "line-cap" => (Repeat("old\n", 30), Repeat("new\n", 30)),
            "byte-cap" => (new string('α', 2000) + "\n", new string('β', 2000) + "\n"),
            "huge-line" => (new string('x', 20_000), new string('y', 20_000)),
            "utf8-input-cap" => (new string('α', 5000), new string('β', 5000)),
            _ => (Repeat("x\n", 250), Repeat("y\n", 250)),
        };
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, expected, TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, current, TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.False(error.DiffComplete);
        Assert.True(error.Diff.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length <= 20);
        Assert.True(Encoding.UTF8.GetByteCount(error.Diff) <= 2048);
        Assert.Contains(detail, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(expected, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(current, error.Message, StringComparison.Ordinal);
        Assert.Equal(current, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        const string Footer = "Diff preview truncated (capped at 20 lines and 2 KiB); not a complete diff.";
        if (detail.Contains("exceed", StringComparison.Ordinal))
        {
            Assert.Contains("incomplete", error.Message, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.Diff);
            Assert.DoesNotContain("--- stored Match.text", error.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.NotEqual(string.Empty, error.Diff);
            Assert.Contains("\nDiff preview:\n" + error.Diff, error.Message, StringComparison.Ordinal);
            Assert.EndsWith(error.Diff + (error.Diff.EndsWith('\n') ? string.Empty : "\n") + Footer, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Footer, error.Diff, StringComparison.Ordinal);
            if (kind == "byte-cap")
            {
                Assert.False(error.Diff.EndsWith('\n'));
                Assert.True(Encoding.UTF8.GetByteCount(error.Diff) >= 2047);
            }
        }
    }

    [Fact]
    public async Task ReplaceAsync_MissingFinalNewline_ExplainsEofInTheDiff()
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "old", TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, "new", TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal(2, error.Diff.Split("No newline at end of file").Length - 1);
        Assert.True(error.DiffComplete);
    }

    [Theory]
    [InlineData("window")]
    [InlineData("slice")]
    public async Task ReplaceAsync_StaleProducerMatch_NeverWrites(string producer)
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "first\ncaptured\nthird\n", TestContext.Current.CancellationToken);
        var anchor = producer == "window"
            ? await _shell.ReadAsync("file.txt", (2, 3), TestContext.Current.CancellationToken)
            : (await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken)).Slice(2, 3);
        await File.WriteAllBytesAsync(path, "first\nexternally changed\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceAsync_AppendedUnterminatedLine_IsDetected()
    {
        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "captured", TestContext.Current.CancellationToken);
        var anchor = await _shell.ReadAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, "captured more text", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal("captured more text", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceAsync_SearchMatch_StaleNeverWrites()
    {
        if (!(Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator).Any(d => File.Exists(Path.Combine(d, "rg"))))
        {
            Assert.Skip("needs rg on PATH");
        }

        var path = PathOf("file.txt");
        await File.WriteAllTextAsync(path, "first\ncaptured\nthird\n", TestContext.Current.CancellationToken);
        var result = await _shell.RunAsync("grep -n captured file.txt", cancellationToken: TestContext.Current.CancellationToken);
        var anchor = Assert.Single(result.Matches!);
        await File.WriteAllTextAsync(path, "first\nexternally changed\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<StaleMatchException>(() => _shell.ReplaceAsync(anchor, "replacement", TestContext.Current.CancellationToken));

        Assert.Equal("first\nexternally changed\n", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
}
