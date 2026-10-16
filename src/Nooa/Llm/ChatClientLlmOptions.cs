// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Llm;

/// <summary>Settings of <see cref="ChatClientLlmClient"/>. The API key belongs to the wrapped chat client, never here.</summary>
public sealed record ChatClientLlmOptions
{
    /// <summary>The default for <see cref="MaxOutputTokens"/>.</summary>
    public const int DefaultMaxOutputTokens = 8192;

    /// <summary>The default for <see cref="RequestTimeout"/>, as upstream (600 s).</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(600);

    /// <summary>The model name to request. Null leaves the choice to the wrapped chat client.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// The token budget for the reply of one call. It limits cost and runaway output. Null sends no limit.
    /// </summary>
    public int? MaxOutputTokens { get; init; } = DefaultMaxOutputTokens;

    /// <summary>The model context window in tokens, or null when it is unknown.</summary>
    public int? ContextWindow { get; init; }

    /// <summary>Wall-clock limit of one attempt. A timeout is retried like any other timeout. Null disables the limit.</summary>
    public TimeSpan? RequestTimeout { get; init; } = DefaultRequestTimeout;

    /// <summary>Retry settings. Null makes one attempt only.</summary>
    public RetryOptions? Retry { get; init; } = new();

    /// <summary>Move <c>&lt;think&gt;...&lt;/think&gt;</c> text of open models into the reasoning.</summary>
    public bool ExtractThinkTags { get; init; }

    /// <summary>Request OpenAI strict mode for a tool when its schema allows it. Otherwise the schema is loose.</summary>
    public bool StrictTools { get; init; } = true;

    /// <summary>Checks the values.</summary>
    /// <exception cref="Errors.ConfigValidationException">A value is out of range.</exception>
    public void Validate()
    {
        if (MaxOutputTokens is <= 0)
        {
            throw new Errors.ConfigValidationException("MaxOutputTokens must be positive or null");
        }

        if (ContextWindow is <= 0)
        {
            throw new Errors.ConfigValidationException("ContextWindow must be positive or null");
        }

        if (RequestTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new Errors.ConfigValidationException("RequestTimeout must be positive or null");
        }
    }
}
