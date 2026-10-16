// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

namespace Nooa.ContextBlocks;

/// <summary>
/// Result of a tool call, stored as a child of <see cref="ToolCallEvent"/>. It is a nested value, not an event.
/// One <see cref="ToolCallEvent"/> with a nested result holds both the call and its result.
/// </summary>
public sealed record ToolResult
{
    /// <summary>Identifier of the tool call that this is the result for.</summary>
    public required string ToolCallId { get; init; }

    /// <summary>Provider-visible result text. It must not change after an LLM generation observes it.</summary>
    public required string Content { get; init; }

    /// <summary>Execution lifecycle status. It is not part of the provider-visible result content.</summary>
    public ResultStatus ResultStatus { get; init; } = ResultStatus.Complete;
}
