// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/middleware.py

using Nooa.Llm;

namespace Nooa.Runtime;

/// <summary>Context for middleware that wraps one LLM round-trip (<see cref="ActorRuntime.GenerateAsync"/>).</summary>
public sealed class LlmCallContext
{
    /// <summary>The input messages. Middleware may replace the list.</summary>
    public required IReadOnlyList<LlmMessage> Messages { get; set; }

    /// <summary>The tools that the model may call. Middleware may replace the list.</summary>
    public IReadOnlyList<ToolDefinition>? Tools { get; set; }

    /// <summary>The structured output type, or null for free text.</summary>
    public Type? OutputType { get; set; }

    /// <summary>The agent that owns the runtime.</summary>
    public required Agent Agent { get; init; }

    /// <summary>The runtime that makes the call.</summary>
    public required ActorRuntime Runtime { get; init; }

    /// <summary>The effective client of the call, including method-level overrides. Read-only.</summary>
    public required ILlmClient Client { get; init; }

    /// <summary>The response. Null on the way in. The innermost handler sets it on the way out.</summary>
    public LlmResponse? Response { get; set; }
}
