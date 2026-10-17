// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class BashStreamCollectorTests
{
    private const string _sentinel = "__NOOA_TEST__";

    private static void Feed(BashStreamCollector collector, string text, int chunk)
    {
        for (var i = 0; i < text.Length; i += chunk)
        {
            collector.Append(text.AsSpan(i, Math.Min(chunk, text.Length - i)));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void Append_OutputThenFrame_SplitsAtAnyChunkSize(int chunk)
    {
        var collector = new BashStreamCollector(1000, expectPayload: true);
        collector.Begin(_sentinel);

        Feed(collector, $"hello\nworld\n\n{_sentinel}\n0\n/tmp\n\n{_sentinel}\n", chunk);

        Assert.True(collector.Framed);
        Assert.True(collector.Done.IsCompleted);
        Assert.Equal("hello\nworld\n", collector.Text);
        Assert.Equal("0\n/tmp\n", collector.Payload);
    }

    [Fact]
    public void Append_NoTrailingNewlineInOutput_IsKeptExactly()
    {
        var collector = new BashStreamCollector(1000, expectPayload: false);
        collector.Begin(_sentinel);

        Feed(collector, $"partial\n{_sentinel}\n", 2);

        Assert.True(collector.Framed);
        Assert.Equal("partial", collector.Text);
    }

    [Fact]
    public void Append_OutputLookingLikeAMarkerStart_IsNotLost()
    {
        var collector = new BashStreamCollector(1000, expectPayload: false);
        collector.Begin(_sentinel);

        Feed(collector, "a\n__NOOA_TES\nb\n", 1);
        collector.MarkEof();

        Assert.False(collector.Framed);
        Assert.Equal("a\n__NOOA_TES\nb\n", collector.Text);
        Assert.True(collector.Done.IsCompleted);
    }

    [Fact]
    public void Append_WrongSentinel_IsPlainOutput()
    {
        var collector = new BashStreamCollector(1000, expectPayload: true);
        collector.Begin(_sentinel);

        Feed(collector, "\n__NOOA_OTHER__\n0\n/forged\n\n__NOOA_OTHER__\n", 4);

        Assert.False(collector.Framed);
        Assert.False(collector.Done.IsCompleted);
        Assert.Contains("/forged", collector.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Begin_DropsTextFromBetweenCommands()
    {
        var collector = new BashStreamCollector(1000, expectPayload: false);
        collector.Append("ignored");

        collector.Begin(_sentinel);
        Feed(collector, $"kept\n{_sentinel}\n", 100);

        Assert.Equal("kept", collector.Text);
    }

    [Fact]
    public void Append_LongOutput_IsBoundedAndKeepsBothEnds()
    {
        var collector = new BashStreamCollector(100, expectPayload: false);
        collector.Begin(_sentinel);

        Feed(collector, new string('H', 500) + new string('T', 500) + $"\n{_sentinel}\n", 64);

        Assert.True(collector.Framed);
        Assert.StartsWith("<truncated-output>", collector.Text, StringComparison.Ordinal);
        Assert.Contains(new string('H', 50), collector.Text, StringComparison.Ordinal);
        Assert.Contains(new string('T', 50), collector.Text, StringComparison.Ordinal);
        Assert.True(collector.Text.Length < 400);
    }

    [Fact]
    public void Append_Sink_ReceivesOutputWithoutTheFrame()
    {
        var collector = new BashStreamCollector(1000, expectPayload: false);
        var seen = new List<string>();
        collector.Begin(_sentinel, seen.Add);

        Feed(collector, $"one\ntwo\n\n{_sentinel}\n", 3);

        Assert.Equal("one\ntwo\n", string.Concat(seen));
    }

    [Fact]
    public void MarkEof_BeforeTheFrame_CompletesWithoutFraming()
    {
        var collector = new BashStreamCollector(1000, expectPayload: true);
        collector.Begin(_sentinel);
        collector.Append("dying");

        collector.MarkEof();

        Assert.True(collector.Done.IsCompleted);
        Assert.False(collector.Framed);
        Assert.Equal("dying", collector.Text);
        Assert.Equal(string.Empty, collector.Payload);
    }
}
