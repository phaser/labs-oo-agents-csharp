// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

using System.Globalization;
using System.Text.Json.Nodes;

namespace Nooa.Llm;

/// <summary>Normalized usage reported for one successful LLM response.</summary>
public sealed record LlmUsage
{
    /// <summary>Total input tokens reported by the provider.</summary>
    public int InputTokens { get; init; }

    /// <summary>Total output tokens reported by the provider.</summary>
    public int OutputTokens { get; init; }

    /// <summary>Input tokens read from the provider prompt cache.</summary>
    public int CachedInputTokens { get; init; }

    /// <summary>Input tokens written to the provider prompt cache.</summary>
    public int CacheWriteInputTokens { get; init; }

    /// <summary>Output tokens that the provider attributes to reasoning.</summary>
    public int ReasoningTokens { get; init; }

    /// <summary>Total input and output tokens reported by the provider.</summary>
    public int TotalTokens { get; init; }

    /// <summary>Estimated call cost in US dollars, when available.</summary>
    public double CostUsd { get; init; }

    /// <summary>
    /// Normalizes the common provider and LiteLLM usage shapes. Returns null when <paramref name="value"/> is null.
    /// A missing or zero field falls through to the next alias, as upstream does.
    /// </summary>
    public static LlmUsage? FromProvider(JsonNode? value)
    {
        if (value is not JsonObject usage)
        {
            return null;
        }

        var promptDetails = First(usage, "prompt_tokens_details", "input_tokens_details");
        var completionDetails = First(usage, "completion_tokens_details", "output_tokens_details");
        var input = ToLong(First(usage, "input_tokens", "prompt_tokens"));
        var output = ToLong(First(usage, "output_tokens", "completion_tokens"));

        var cached = NonZero(First(usage, "cached_input_tokens", "cached_tokens", "cache_read_input_tokens"))
            ?? NonZero(First(promptDetails, "cached_tokens", "cache_read_input_tokens"));
        var cacheWrite = NonZero(First(usage, "cache_write_input_tokens", "cache_creation_input_tokens"))
            ?? NonZero(First(promptDetails, "cache_write_tokens", "cache_write_input_tokens", "cache_creation_input_tokens"));
        var reasoning = NonZero(First(usage, "reasoning_tokens"))
            ?? NonZero(First(completionDetails, "reasoning_tokens"));
        var total = NonZero(First(usage, "total_tokens")) ?? (input + output);
        var cost = ToDouble(First(usage, "cost_usd", "cost"));

        return new LlmUsage
        {
            InputTokens = (int)input,
            OutputTokens = (int)output,
            CachedInputTokens = (int)(cached ?? 0),
            CacheWriteInputTokens = (int)(cacheWrite ?? 0),
            ReasoningTokens = (int)(reasoning ?? 0),
            TotalTokens = (int)total,
            CostUsd = cost,
        };
    }

    private static JsonNode? First(JsonNode? source, params string[] keys)
    {
        if (source is not JsonObject obj)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (obj[key] is { } result)
            {
                return result;
            }
        }

        return null;
    }

    private static long? NonZero(JsonNode? node)
    {
        var number = ToLong(node);
        return number == 0 ? null : number;
    }

    private static long ToLong(JsonNode? node) => node is JsonValue v ? (long)ToDoubleValue(v) : 0;

    private static double ToDouble(JsonNode? node) => node is JsonValue v ? ToDoubleValue(v) : 0.0;

    private static double ToDoubleValue(JsonValue value)
    {
        if (value.TryGetValue<double>(out var d))
        {
            return d;
        }

        if (value.TryGetValue<long>(out var l))
        {
            return l;
        }

        if (value.TryGetValue<string>(out var s)
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0.0;
    }
}
