// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// A turn where the model replied with plain text instead of a tool call.
/// The event is recorded so it can be queried and replayed, but it has the metadata role,
/// so the model never sees it and capturing it does not change generation.
/// </summary>
public sealed record TextOnlyReply() : EventBase(Role.Metadata)
{
    /// <summary>Verbatim text that the model emitted. The renderer must not truncate it.</summary>
    [MaxString]
    public string Content { get; init; } = "";

    /// <summary>The <c>finish_reason</c> of the text-only turn, for example <c>stop</c>.</summary>
    public string FinishReason { get; init; } = "";

    /// <summary>Qualified name of the text-only response handler.</summary>
    public string Handler { get; init; } = "";

    /// <summary>Handler action: <c>return_result</c>, <c>retry</c> or <c>tool_calls</c>.</summary>
    public string Action { get; init; } = "";

    /// <summary>Count of consecutive text-only turns, including this one.</summary>
    public int ConsecutiveTextOnly { get; init; }
}
