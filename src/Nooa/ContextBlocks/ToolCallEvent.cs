// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

using System.Text.Json.Nodes;

namespace Nooa.ContextBlocks;

/// <summary>
/// Tool invocation event with an optional nested result.
/// One event holds both the call and its result, so a call or a result cannot be orphaned,
/// and archiving the event archives the whole interaction.
/// </summary>
public sealed record ToolCallEvent() : EventBase(Role.Assistant)
{
    /// <summary>Unique identifier of this tool call.</summary>
    public required string ToolCallId { get; init; }

    /// <summary>Name of the tool that is called.</summary>
    public required string Name { get; init; }

    /// <summary>Arguments passed to the tool.</summary>
    public required JsonObject Arguments { get; init; }

    /// <summary>The <see cref="Nooa.Llm.LlmResponse"/> event that emitted this tool call. Not shown to the model.</summary>
    [Hidden]
    public string? LlmResponseId { get; init; }

    /// <summary>Result of the tool call. It is set after the tool runs.</summary>
    public ToolResult? Result { get; set; }
}
