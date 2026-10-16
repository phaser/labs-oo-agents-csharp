// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Emitted immediately before the LLM round trip. It is for telemetry and activity display, for example a
/// status line that shows "waiting on model". It pairs one-to-one with a later <see cref="LlmCallEnd"/>
/// that has the same generation identifier and turn number. It is never recorded. Persisted as <c>LLMCallStart</c>.
/// </summary>
public sealed record LlmCallStart() : EventBase(Role.RuntimeEvent)
{
    /// <summary>Name of the method that is generated.</summary>
    public required string MethodName { get; init; }

    /// <summary>Strategy name.</summary>
    public required string Strategy { get; init; }

    /// <summary>Generation session identifier.</summary>
    public required string GenerationId { get; init; }

    /// <summary>Turn number within this generation session (1, 2, 3, ...).</summary>
    public required int TurnNumber { get; init; }
}
