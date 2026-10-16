// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/limits.py (no upstream test file)

using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ContextLimitsTests
{
    [Fact]
    public void Constructor_CapLeavesNoRoom_Throws()
    {
        var e = Assert.Throws<ConfigValidationException>(() => new ContextLimits(1000, 1000));

        Assert.Contains("leaves no room for input", e.Message);
    }

    [Fact]
    public void Constructor_FallbackReserveAboveWindow_IsAllowed()
    {
        var limits = new ContextLimits(1000, 5000, reserveIsFallback: true);

        Assert.Equal(1, limits.UsableInputTokens);
    }

    [Fact]
    public void UsableInputTokens_UnknownWindow_IsNull()
    {
        Assert.Null(new ContextLimits(null, 100).UsableInputTokens);
    }

    [Fact]
    public void UsableInputTokens_KnownWindow_SubtractsReserve()
    {
        Assert.Equal(900, new ContextLimits(1000, 100).UsableInputTokens);
    }

    [Fact]
    public void For_ExplicitCap_IsNotFallback()
    {
        var limits = ContextLimits.For(new FakeLlmClient(), replyCap: 50, fallbackReserve: 10);

        Assert.Equal(50, limits.ReservedOutputTokens);
        Assert.False(limits.ReserveIsFallback);
    }

    [Fact]
    public void For_NoCap_UsesFallbackReserve()
    {
        var limits = ContextLimits.For(new FakeLlmClient(), fallbackReserve: 10);

        Assert.Equal(10, limits.ReservedOutputTokens);
        Assert.True(limits.ReserveIsFallback);
    }

    [Fact]
    public void ReplyCapKeys_NamesTheThreeProviderSpellings()
    {
        Assert.Equal(["max_completion_tokens", "max_output_tokens", "max_tokens"], ContextLimits.ReplyCapKeys.Order());
    }
}
