// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Output from running a REPL cell. Upstream names this event <c>PythonOutput</c>.
/// </summary>
/// <remarks>
/// The event holds the output of the code run, separate from the tool result status.
/// This allows nested agent calls without breaking the message order that providers require
/// (a tool result must follow its tool call immediately).
/// </remarks>
public sealed record CellOutput() : EventBase(Role.User)
{
    /// <summary>Identifier of the tool call that produced this output.</summary>
    public required string ToolCallId { get; init; }

    /// <summary>Execution status: complete, error or cancelled.</summary>
    public required ResultStatus ExecutionStatus { get; init; }

    /// <summary>Jupyter-style execution count (1, 2, 3, ...) for <c>Out[n]</c> access. Not shown to the model.</summary>
    [Hidden]
    public required int ExecutionCount { get; init; }

    /// <summary>Captured standard output. The renderer must not truncate it.</summary>
    [MaxString]
    public string Stdout { get; init; } = "";

    /// <summary>Captured standard error. The renderer must not truncate it.</summary>
    [MaxString]
    public string Stderr { get; init; } = "";

    /// <summary>Formatted error message if execution failed.</summary>
    public string Error { get; init; } = "";

    /// <summary>
    /// The value that the cell returned, as JSON. The caller must convert the value to JSON.
    /// </summary>
    public JsonNode? Value { get; init; }

    /// <summary>True if <see cref="Value"/> comes from an explicit <c>return x</c>, false if it is implicit. Not shown to the model.</summary>
    [Hidden]
    public bool ExplicitReturn { get; init; }

    /// <summary>Image content blocks captured by <c>show()</c> during execution. Not shown to the model.</summary>
    [Hidden]
    public IReadOnlyList<JsonNode> Images { get; init; } = [];
}
