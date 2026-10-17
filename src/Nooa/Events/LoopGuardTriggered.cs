// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// The CodeAct loop guard detected a repeated tool call.
/// <see cref="Content"/> is the fixed-template message that the model sees. The other properties are trace data.
/// </summary>
public sealed record LoopGuardTriggered() : EventBase(Role.User)
{
    /// <summary>Loop-guard message shown to the model.</summary>
    public required string Content { get; init; }

    /// <summary><c>blocked</c>, <c>nudged</c> or <c>stopped</c>. Not shown to the model.</summary>
    [Hidden]
    public string Action { get; init; } = "";

    /// <summary>Repeated tool name. Not shown to the model.</summary>
    [Hidden]
    public string ToolName { get; init; } = "";

    /// <summary>Triggering tool call identifier. Not shown to the model.</summary>
    [Hidden]
    public string ToolCallId { get; init; } = "";

    /// <summary>Identical calls in the window, including this one. Not shown to the model.</summary>
    [Hidden]
    public int Repeats { get; init; }

    /// <summary>Number of tool calls compared. Not shown to the model.</summary>
    [Hidden]
    public int Window { get; init; }

    /// <summary>Hash of the normalized action. Not shown to the model.</summary>
    [Hidden]
    public string Fingerprint { get; init; } = "";
}
