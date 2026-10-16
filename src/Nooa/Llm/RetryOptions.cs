// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/retry_config.py

namespace Nooa.Llm;

/// <summary>Retry behaviour for LLM API calls.</summary>
public sealed record RetryOptions
{
    /// <summary>The HTTP status codes that upstream retries by default.</summary>
    public static readonly IReadOnlySet<int> DefaultRetryableStatusCodes =
        new HashSet<int> { 408, 429, 500, 502, 503, 504 };

    /// <summary>Retries after the first attempt, for errors other than rate limits.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Delay before the first retry.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper limit of the backoff delay, before jitter.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Growth factor of the delay for each attempt.</summary>
    public double ExponentialBase { get; init; } = 2.0;

    /// <summary>The delay grows by up to this fraction at random.</summary>
    public double JitterFactor { get; init; } = 0.3;

    /// <summary>Retries in addition to <see cref="MaxRetries"/> for HTTP 429.</summary>
    public int RateLimitExtraRetries { get; init; } = 3;

    /// <summary>Delay before the first retry of a rate-limited call.</summary>
    public TimeSpan RateLimitBaseDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Growth factor of the delay for rate-limited calls.</summary>
    public double RateLimitBackoffBase { get; init; } = 3.0;

    /// <summary>The HTTP status codes that are worth a retry. A code that is not here is never retried.</summary>
    public IReadOnlySet<int> RetryableStatusCodes { get; init; } = DefaultRetryableStatusCodes;

    /// <summary>Retry when the model returns reasoning but no final content.</summary>
    public bool RetryOnEmptyContent { get; init; }

    /// <summary>Called before each retry with the attempt number (from 1), the error and the delay.</summary>
    public Action<int, Exception, TimeSpan>? OnRetry { get; init; }
}
