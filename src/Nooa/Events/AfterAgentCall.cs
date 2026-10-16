// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Completion of an agent method (success or exception). It is symmetric with <see cref="BeforeAgentCall"/>
/// and is never recorded.
/// </summary>
public sealed record AfterAgentCall() : EventBase(Role.RuntimeEvent)
{
    /// <summary>Name of the agent method that was invoked.</summary>
    public required string MethodName { get; init; }

    /// <summary>Unique identifier of this agent-method call.</summary>
    public required string CallId { get; init; }

    /// <summary>Nearest traced ancestor call identifier, or null at the top level.</summary>
    public string? ParentCallId { get; init; }

    /// <summary>True if no agent was active before this call.</summary>
    public required bool IsTopLevel { get; init; }

    /// <summary>True for LLM-run methods, false for plain code and sync methods.</summary>
    public bool NeedsGeneration { get; init; }

    /// <summary>True if the method returned normally.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Exception type name if the method threw.</summary>
    public string? ExceptionType { get; init; }
}
