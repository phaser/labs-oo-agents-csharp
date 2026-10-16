// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Emitted before each LLM generation turn, in the strategy loops. It triggers the event management policies
/// before each turn. It is never recorded in conversation events.
/// </summary>
public sealed record BeforeTurn() : EventBase(Role.RuntimeEvent)
{
    /// <summary>Name of the method that is generated.</summary>
    public required string MethodName { get; init; }

    /// <summary>Strategy name.</summary>
    public required string Strategy { get; init; }

    /// <summary>Generation session identifier.</summary>
    public required string GenerationId { get; init; }

    /// <summary>Parent generation identifier if nested.</summary>
    public string? ParentGenerationId { get; init; }

    /// <summary>Turn number within this generation session (1, 2, 3, ...).</summary>
    public required int TurnNumber { get; init; }
}
