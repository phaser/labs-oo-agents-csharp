// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Emitted immediately after the LLM round trip returns or fails. It is symmetric with <see cref="LlmCallStart"/>.
/// <see cref="Success"/> is false and <see cref="ExceptionType"/> is set when the call threw, so observers can
/// clear their "waiting on model" state. It is never recorded. Persisted as <c>LLMCallEnd</c>.
/// </summary>
public sealed record LlmCallEnd() : EventBase(Role.RuntimeEvent)
{
    /// <summary>Name of the method that is generated.</summary>
    public required string MethodName { get; init; }

    /// <summary>Strategy name.</summary>
    public required string Strategy { get; init; }

    /// <summary>Generation session identifier.</summary>
    public required string GenerationId { get; init; }

    /// <summary>Turn number within this generation session (1, 2, 3, ...).</summary>
    public required int TurnNumber { get; init; }

    /// <summary>True if the LLM call returned normally.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Exception type name if the call threw.</summary>
    public string? ExceptionType { get; init; }
}
