// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_llm_types.py

using System.Text.Json.Nodes;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class LlmUsageTests
{
    [Fact]
    public void FromProvider_OpenAiShape_NormalizesCacheAndReasoning()
    {
        var usage = LlmUsage.FromProvider(JsonNode.Parse("""
            {"prompt_tokens":100,"completion_tokens":20,
             "prompt_tokens_details":{"cached_tokens":75},
             "completion_tokens_details":{"reasoning_tokens":8},
             "total_tokens":120}
            """));

        Assert.Equal(
            new LlmUsage { InputTokens = 100, OutputTokens = 20, CachedInputTokens = 75, ReasoningTokens = 8, TotalTokens = 120 },
            usage);
    }

    [Fact]
    public void FromProvider_AnthropicShape_ComputesTotal()
    {
        var usage = LlmUsage.FromProvider(JsonNode.Parse("""
            {"input_tokens":90,"output_tokens":10,"cache_read_input_tokens":60,"cache_creation_input_tokens":15}
            """));

        Assert.Equal(
            new LlmUsage { InputTokens = 90, OutputTokens = 10, CachedInputTokens = 60, CacheWriteInputTokens = 15, TotalTokens = 100 },
            usage);
    }

    [Fact]
    public void FromProvider_CostAlias_IsRead()
    {
        var usage = LlmUsage.FromProvider(JsonNode.Parse("""{"prompt_tokens":1,"cached_tokens":30,"cost":0.0042}"""))!;

        Assert.Equal(0.0042, usage.CostUsd);
        Assert.Equal(30, usage.CachedInputTokens);
    }

    [Fact]
    public void FromProvider_ZeroTopLevelFallsThroughToDetails()
    {
        var usage = LlmUsage.FromProvider(JsonNode.Parse("""
            {"cached_tokens":0,"prompt_tokens_details":{"cached_tokens":5}}
            """))!;

        Assert.Equal(5, usage.CachedInputTokens);
    }

    [Fact]
    public void FromProvider_NullOrNotAnObject_ReturnsNull()
    {
        Assert.Null(LlmUsage.FromProvider(null));
        Assert.Null(LlmUsage.FromProvider(JsonValue.Create(3)));
    }
}
