// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Emitted when an agent method invocation begins (generation, plain code or sync).
/// It is symmetric with <see cref="BeforeTurn"/> and is never recorded.
/// </summary>
/// <remarks>
/// <see cref="IsTopLevel"/> is the agent-instance nesting axis (no agent was active before this call).
/// Subscribers that want the outermost run filter on it, not on <see cref="ParentCallId"/>.
/// </remarks>
public sealed record BeforeAgentCall() : EventBase(Role.RuntimeEvent)
{
    /// <summary>Name of the agent method that is invoked.</summary>
    public required string MethodName { get; init; }

    /// <summary>Unique identifier of this agent-method call.</summary>
    public required string CallId { get; init; }

    /// <summary>Nearest traced ancestor call identifier, or null at the top level.</summary>
    public string? ParentCallId { get; init; }

    /// <summary>True if no agent was active before this call.</summary>
    public required bool IsTopLevel { get; init; }

    /// <summary>True for LLM-run methods, false for plain code and sync methods.</summary>
    public bool NeedsGeneration { get; init; }
}
