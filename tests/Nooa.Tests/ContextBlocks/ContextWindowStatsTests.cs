// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_context_stats.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class ContextWindowStatsTests
{
    private static ContextWindowStats Stats(int? prompt = null, int blockChars = 0, int eventChars = 0) => new()
    {
        ContextBlocksCount = 6,
        EventsCount = 18,
        PromptTokens = prompt,
        ContextBlocksChars = blockChars,
        EventsChars = eventChars,
    };

    [Fact]
    public void Tokens_BeforeFirstResponse_AreNull()
    {
        var stats = Stats(blockChars: 100, eventChars: 100);

        Assert.Null(stats.TotalTokens);
        Assert.Null(stats.ContextBlocksTokens);
        Assert.Null(stats.EventsTokens);
        Assert.Null(stats.OverallUtilization);
    }

    [Fact]
    public void Tokens_AreAttributedByCharacterShareAndAddUp()
    {
        var stats = Stats(prompt: 1000, blockChars: 300, eventChars: 700);

        Assert.Equal(300, stats.ContextBlocksTokens);
        Assert.Equal(700, stats.EventsTokens);
        Assert.Equal(stats.PromptTokens, stats.ContextBlocksTokens + stats.EventsTokens);
    }

    [Fact]
    public void Tokens_WithRounding_StillAddUp()
    {
        var stats = Stats(prompt: 101, blockChars: 1, eventChars: 2);

        Assert.Equal(101, stats.ContextBlocksTokens + stats.EventsTokens);
    }

    [Fact]
    public void ContextBlocksTokens_NoCharacters_IsZero() => Assert.Equal(0, Stats(prompt: 50).ContextBlocksTokens);

    [Fact]
    public void EffectiveWindow_SubtractsTheOutputReserve()
    {
        var stats = Stats(prompt: 10) with { ModelContextWindow = 100_000, ReservedOutputTokens = 4_000 };

        Assert.Equal(96_000, stats.EffectiveWindow);
        Assert.Equal(10 / 96_000.0, stats.OverallUtilization);
    }

    [Fact]
    public void EffectiveWindow_UnknownWindow_IsNull() => Assert.Null(Stats(prompt: 10).EffectiveWindow);

    [Fact]
    public void EffectiveWindow_ReserveLargerThanWindow_IsOne() =>
        Assert.Equal(1, (Stats() with { ModelContextWindow = 10, ReservedOutputTokens = 50 }).EffectiveWindow);

    [Fact]
    public void Utilization_CanExceedOne()
    {
        var stats = Stats(prompt: 120) with { ModelContextWindow = 100, ReservedOutputTokens = 10 };

        Assert.True(stats.OverallUtilization > 1.0);
    }

    [Fact]
    public void Format_BeforeFirstResponse_SaysAwaiting()
    {
        var text = Stats().Format();

        Assert.StartsWith("Context: awaiting first model response\n", text);
        Assert.Contains("Events.Collapse", text);
    }

    [Fact]
    public void Format_WithWindow_ShowsTotalAndAttribution()
    {
        var stats = Stats(prompt: 12_450, blockChars: 6, eventChars: 4) with { ModelContextWindow = 200_000 };

        var text = stats.Format();

        Assert.Contains("Context: 12,450 / 200,000 tokens (6.2%)", text);
        Assert.Contains("  Context blocks: ~7,470 tokens — 6 blocks", text);
        Assert.Contains("  Events:         ~4,980 tokens — 18 events", text);
        Assert.DoesNotContain("nearly full", text);
    }

    [Fact]
    public void Format_WithReserve_ShowsUsableTokensAndLabel()
    {
        var stats = Stats(prompt: 1000) with { ModelContextWindow = 10_000, ReservedOutputTokens = 2_000 };

        Assert.Contains("Context: 1,000 / 8,000 usable tokens (12.5%) · output reserve: 2,000", stats.Format());
        Assert.Contains("planning reserve: 2,000", (stats with { OutputReserveIsFallback = true }).Format());
    }

    [Fact]
    public void Format_UnknownWindow_ShowsOnlyTheTotal() =>
        Assert.Contains("Context: 1,000 tokens\n", Stats(prompt: 1000).Format());

    [Fact]
    public void Format_Hot_Warns()
    {
        var stats = Stats(prompt: 90) with { ModelContextWindow = 100 };

        Assert.Contains("Context is nearly full.", stats.Format());
    }

    [Fact]
    public void Format_WithDroppedBlocksAndEvents_ShowsCounts()
    {
        var stats = Stats(prompt: 10) with { ContextBlocksDropped = 2, EventsDropped = 3 };

        var text = stats.Format();

        Assert.Contains("2 EVICTED", text);
        Assert.Contains("3 dropped", text);
        Assert.Contains("Context is nearly full.", text);
    }
}
