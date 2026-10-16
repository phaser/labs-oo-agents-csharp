// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class SummaryTests
{
    [Fact]
    public void RoundTrip_KeepsRangeChildrenAndText()
    {
        var summary = new Summary
        {
            SummaryTag = "2..40",
            ReplacedRange = new SeqRange(2, 40),
            ChildrenTags = ["2", "3", "4"],
            SummaryText = "The agent explored the database schema.",
            Doc = "hint",
        };

        var back = EventJson.RoundTrip(summary);

        Assert.Equal("Summary", back.EventType);
        Assert.Null(back.Tag);
        Assert.Equal("2..40", back.SummaryTag);
        Assert.Equal(new SeqRange(2, 40), back.ReplacedRange);
        Assert.Equal(["2", "3", "4"], back.ChildrenTags);
        Assert.Equal("The agent explored the database schema.", back.SummaryText);
        Assert.Equal("hint", back.Doc);
    }

    [Fact]
    public void Serialize_RangeIsAnArray()
    {
        var json = EventJson.Write(new Summary { SummaryTag = "2..4", ReplacedRange = new SeqRange(2, 4) });

        Assert.Contains("\"replaced_range\":[2,4]", json);
    }

    [Fact]
    public void Defaults_NoSummaryTextMeansTruncation()
    {
        var summary = new Summary { SummaryTag = "1..5", ReplacedRange = new SeqRange(1, 5) };

        Assert.Null(summary.SummaryText);
        Assert.Empty(summary.ChildrenTags);
        Assert.Equal(Role.Assistant, summary.EventRole);
    }

    [Fact]
    public void Tag_CanBeSetToTheSummaryTag()
    {
        var summary = new Summary { SummaryTag = "2..40", ReplacedRange = new SeqRange(2, 40) };

        summary.Tag = summary.SummaryTag;

        Assert.Equal("2..40", summary.Tag);
    }
}
