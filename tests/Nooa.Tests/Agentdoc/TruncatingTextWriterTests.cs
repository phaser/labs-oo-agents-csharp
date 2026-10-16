// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_truncating_stream.py

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class TruncatingTextWriterTests
{
    private static TruncatingTextWriter Writer(int limit, int? tail = null) => new(limit, tail);

    [Fact]
    public void Write_SmallText_StaysInHead()
    {
        using var buf = Writer(100);
        buf.Write("hello");
        Assert.Equal("hello", buf.ToString());
        Assert.False(buf.WasTruncated);
    }

    [Fact]
    public void Write_ManySmallWrites_Accumulate()
    {
        using var buf = Writer(100);
        buf.Write("abc");
        buf.Write('d');
        buf.Write(new[] { 'e', 'f' }, 0, 2);
        Assert.Equal("abcdef", buf.ToString());
    }

    [Fact]
    public void Write_ExactlyAtLimit_IsNotTruncated()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 10));
        Assert.False(buf.WasTruncated);
        Assert.Equal(10L, buf.CharsWritten);
        Assert.Equal(new string('x', 10), buf.ToString());
    }

    [Fact]
    public void Write_OneOverLimit_IsTruncated()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 11));
        Assert.True(buf.WasTruncated);
        Assert.Equal(11L, buf.CharsWritten);
    }

    [Fact]
    public void Write_EmptyText_DoesNothing()
    {
        using var buf = Writer(10);
        buf.Write(string.Empty);
        buf.Write((string?)null);
        Assert.Equal(string.Empty, buf.ToString());
        Assert.False(buf.WasTruncated);
        Assert.Equal(0L, buf.CharsWritten);
    }

    [Fact]
    public void Write_Overflow_GoesToTail()
    {
        using var buf = Writer(10);
        buf.Write(new string('A', 5));
        buf.Write(new string('B', 10));
        var value = buf.ToString();
        Assert.StartsWith("<truncated-output>", value, StringComparison.Ordinal);
        Assert.Contains("AAAAA", value, StringComparison.Ordinal);
        Assert.Contains("BBBBB", value, StringComparison.Ordinal);
        Assert.Contains("... 5 chars not shown ...", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_Overflow_RollsOldestTailOut()
    {
        using var buf = Writer(10);
        buf.Write(new string('A', 5));
        buf.Write(new string('B', 5));
        buf.Write(new string('C', 5));
        var value = buf.ToString();
        Assert.Contains("CCCCC", value, StringComparison.Ordinal);
        Assert.DoesNotContain("BBBBB", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_CustomTailChars_ControlsTheTailWindow()
    {
        using var buf = Writer(20, tail: 15);
        buf.Write(new string('A', 5));
        buf.Write(new string('B', 100));
        Assert.Equal(15, buf.ToString().Count(c => c == 'B'));
    }

    [Fact]
    public void Write_ManySmallWrites_KeepsOnlyTheLastTailChars()
    {
        using var buf = Writer(100);
        for (var i = 0; i < 10_000; i++)
        {
            buf.Write(i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";");
        }

        var value = buf.ToString();
        Assert.Contains("9999;", value, StringComparison.Ordinal);
        Assert.Contains("0;1;2;", value, StringComparison.Ordinal);
        Assert.DoesNotContain("5000;", value, StringComparison.Ordinal);
    }

    [Fact]
    public void WasTruncated_SeveralWrites_IsCumulative()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 6));
        buf.Write(new string('y', 6));
        Assert.True(buf.WasTruncated);
    }

    [Fact]
    public void CharsWritten_CountsDroppedCharacters()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 200));
        Assert.Equal(200L, buf.CharsWritten);
    }

    [Fact]
    public void ToString_Truncated_NamesTotalAndNotRecoverable()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 100));
        var value = buf.ToString();
        Assert.Contains("Output too large (100 chars). Showing first 5 and last 5 chars.", value, StringComparison.Ordinal);
        Assert.Contains("The 90 chars in the middle are not recoverable.", value, StringComparison.Ordinal);
        Assert.EndsWith("</truncated-output>", value, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_LargeTotal_UsesThousandsSeparator()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 12_345));
        Assert.Contains("12,345 chars", buf.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_CalledTwice_ReturnsSameText()
    {
        using var buf = Writer(10);
        buf.Write(new string('x', 100));
        Assert.Equal(buf.ToString(), buf.ToString());
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(10, -1)]
    [InlineData(10, 11)]
    public void Constructor_InvalidArguments_Throws(int limit, int? tail)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TruncatingTextWriter(limit, tail));
    }
}
