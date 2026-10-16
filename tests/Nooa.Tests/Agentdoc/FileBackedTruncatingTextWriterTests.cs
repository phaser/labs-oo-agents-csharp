// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_file_backed_truncating_stream.py

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public sealed class FileBackedTruncatingTextWriterTests : IDisposable
{
    private readonly List<FileBackedTruncatingTextWriter> _writers = [];

    public void Dispose()
    {
        foreach (var writer in _writers)
        {
            writer.Cleanup();
        }
    }

    private FileBackedTruncatingTextWriter Create(int limit = 100, int? tail = null, string? directory = null, string prefix = "nemo_output_", string suffix = ".txt")
    {
        var writer = new FileBackedTruncatingTextWriter(limit, tail, directory, prefix, suffix);
        _writers.Add(writer);
        return writer;
    }

    [Fact]
    public void Write_SmallText_IsNotTruncated()
    {
        var buf = Create();
        buf.Write("hello");
        Assert.Equal("hello", buf.ToString());
        Assert.False(buf.WasTruncated);
    }

    [Fact]
    public void Write_OverLimit_ShowsNotice()
    {
        var buf = Create(10);
        buf.Write(new string('x', 100));
        Assert.True(buf.WasTruncated);
        Assert.Equal(100L, buf.CharsWritten);
        Assert.Contains("Output too large (100 chars)", buf.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_Unicode_RoundTripsThroughFile()
    {
        var buf = Create();
        const string Text = "Hello 世界! 🚀 café";
        buf.Write(Text);
        Assert.Equal(Text, buf.ToString());
        Assert.Equal(Text, File.ReadAllText(buf.FilePath!));
    }

    [Fact]
    public void FilePath_AfterCreate_PointsToExistingFile()
    {
        var buf = Create();
        buf.Write("hello");
        Assert.NotNull(buf.FilePath);
        Assert.True(File.Exists(buf.FilePath));
    }

    [Fact]
    public void Write_Truncated_FileHoldsFullOutput()
    {
        var buf = Create(10);
        var content = new string('A', 5) + new string('B', 90) + new string('C', 5);
        buf.Write(content);
        Assert.True(buf.WasTruncated);
        Assert.Equal(content, File.ReadAllText(buf.FilePath!));
    }

    [Fact]
    public void Write_ManyWrites_ConcatenatesInFile()
    {
        var buf = Create(10);
        buf.Write("hello ");
        buf.Write("world ");
        buf.Write("this is a long string");
        Assert.Equal("hello world this is a long string", File.ReadAllText(buf.FilePath!));
    }

    [Fact]
    public void Write_LargeOutput_FileIsComplete()
    {
        var buf = Create(1000);
        var content = new string('x', 50_000);
        buf.Write(content);
        Assert.Equal(content, File.ReadAllText(buf.FilePath!));
    }

    [Fact]
    public void Constructor_CustomDirectoryPrefixSuffix_ShapeTheFileName()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var buf = Create(100, null, dir.FullName, "myprefix_", ".log");
            Assert.Equal(dir.FullName, Path.GetDirectoryName(buf.FilePath));
            Assert.StartsWith("myprefix_", Path.GetFileName(buf.FilePath), StringComparison.Ordinal);
            Assert.EndsWith(".log", buf.FilePath, StringComparison.Ordinal);
        }
        finally
        {
            _writers[^1].Cleanup();
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("../evil_", ".txt")]
    [InlineData("ok_", "/../x")]
    [InlineData("a\\b", ".txt")]
    public void Constructor_PathSeparatorInNameParts_Throws(string prefix, string suffix)
    {
        Assert.Throws<ArgumentException>(() => new FileBackedTruncatingTextWriter(100, null, null, prefix, suffix));
    }

    [Fact]
    public void Constructor_OnUnix_CreatesFileForCurrentUserOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var buf = Create();
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(buf.FilePath!));
    }

    [Fact]
    public void ToString_Truncated_NamesTheFile()
    {
        var buf = Create(10);
        buf.Write(new string('x', 100));
        var value = buf.ToString();
        Assert.StartsWith("<truncated-output>", value, StringComparison.Ordinal);
        Assert.Contains(buf.FilePath!, value, StringComparison.Ordinal);
        Assert.Contains("The full untruncated output (100 chars) is in:", value, StringComparison.Ordinal);
        Assert.Contains("Read that file to see the 90 chars not shown here.", value, StringComparison.Ordinal);
        Assert.Contains("not shown", value, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NotTruncated_HasNoNotice()
    {
        var buf = Create();
        buf.Write("hello");
        Assert.Equal("hello", buf.ToString());
    }

    [Fact]
    public void ToString_Truncated_KeepsHeadAndTail()
    {
        var buf = Create(50);
        buf.Write("HEADDATA" + new string('x', 200) + "TAILCONTENT");
        var value = buf.ToString();
        Assert.Contains("HEADDATA", value, StringComparison.Ordinal);
        Assert.Contains("TAILCONTENT", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_InvalidDirectory_FallsBackToMemory()
    {
        var buf = Create(10, null, Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N"), "xyz"));
        buf.Write(new string('x', 50));
        Assert.Null(buf.FilePath);
        Assert.Equal(50L, buf.CharsWritten);
        var value = buf.ToString();
        Assert.Contains("Output too large", value, StringComparison.Ordinal);
        Assert.DoesNotContain("full untruncated output", value, StringComparison.Ordinal);
        Assert.Contains("not recoverable", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_ClosesFileButKeepsItOnDisk()
    {
        var buf = Create();
        buf.Write("test");
        var path = buf.FilePath!;
        buf.Dispose();
        buf.Dispose();
        Assert.True(File.Exists(path));
        Assert.Equal("test", File.ReadAllText(path));
    }

    [Fact]
    public void ToString_AfterDispose_ReadsMemory()
    {
        var buf = Create(10);
        buf.Write(new string('x', 100));
        buf.Dispose();
        var value = buf.ToString();
        Assert.Contains("Output too large", value, StringComparison.Ordinal);
        Assert.Contains(buf.FilePath!, value, StringComparison.Ordinal);
    }

    [Fact]
    public void Cleanup_RemovesFile()
    {
        var buf = Create();
        buf.Write("test");
        var path = buf.FilePath!;
        buf.Cleanup();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Cleanup_CalledTwice_DoesNotThrow()
    {
        var buf = Create();
        buf.Write("test");
        buf.Cleanup();
        buf.Cleanup();
    }

    [Fact]
    public void Cleanup_ThenToString_StillShowsNotice()
    {
        var buf = Create(10);
        buf.Write(new string('x', 100));
        buf.Cleanup();
        Assert.Contains("Output too large", buf.ToString(), StringComparison.Ordinal);
    }
}
