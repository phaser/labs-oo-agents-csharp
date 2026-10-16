// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_retry_config.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class RetryOptionsTests
{
    [Fact]
    public void Defaults_MatchUpstream()
    {
        var options = new RetryOptions();

        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(1), options.BaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), options.MaxDelay);
        Assert.Equal(2.0, options.ExponentialBase);
        Assert.Equal(0.3, options.JitterFactor);
        Assert.Equal(3, options.RateLimitExtraRetries);
        Assert.Equal(TimeSpan.FromSeconds(3), options.RateLimitBaseDelay);
        Assert.Equal(3.0, options.RateLimitBackoffBase);
        Assert.Equal(new HashSet<int> { 408, 429, 500, 502, 503, 504 }, options.RetryableStatusCodes);
        Assert.False(options.RetryOnEmptyContent);
        Assert.Null(options.OnRetry);
    }

    [Fact]
    public void With_Change_LeavesTheOriginalUnchanged()
    {
        var original = new RetryOptions();

        var changed = original with { RateLimitBackoffBase = 5.0 };

        Assert.Equal(5.0, changed.RateLimitBackoffBase);
        Assert.Equal(3.0, original.RateLimitBackoffBase);
    }
}
