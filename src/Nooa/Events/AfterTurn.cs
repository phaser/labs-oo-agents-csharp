// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Emitted after each LLM generation turn. It is symmetric with <see cref="BeforeTurn"/>.
/// When <see cref="IsFinal"/> is true, this is the last turn and the method is complete.
/// It is never recorded in conversation events.
/// </summary>
public sealed record AfterTurn() : EventBase(Role.RuntimeEvent)
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

    /// <summary>True if this is the final turn (the method is complete).</summary>
    public required bool IsFinal { get; init; }

    /// <summary>True if the method completed successfully. Set only when <see cref="IsFinal"/> is true.</summary>
    public bool? Success { get; init; }

    /// <summary>Exception type if the method failed. Set only when <see cref="IsFinal"/> is true.</summary>
    public string? ExceptionType { get; init; }
}
