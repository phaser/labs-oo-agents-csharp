// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py

using System.Reflection;
using Nooa.Config;

namespace Nooa.Runtime;

/// <summary>Settings of a <see cref="RoslynExecutionEngine"/>.</summary>
public sealed record ReplOptions
{
    /// <summary>The default time limit of one cell (upstream: 90 seconds).</summary>
    public static readonly TimeSpan DefaultCellTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The time limit of one cell. Null means no limit. The limit cancels <see cref="ReplGlobals.Cancellation"/>.
    /// It cannot stop code that does not check the token.
    /// </summary>
    public TimeSpan? CellTimeout { get; init; } = DefaultCellTimeout;

    /// <summary>
    /// The time that the engine waits for a cell to stop after the token is cancelled. If the cell is still running,
    /// the engine gives up the cell, reports an error and refuses all later cells, because the cell still uses the session state.
    /// </summary>
    public TimeSpan AbandonGrace { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The budgets for the captured output and for the error text.</summary>
    public CaptureConfig Capture { get; init; } = TruncationConfig.Default.Capture;

    /// <summary>Assemblies that cells may use, in addition to the defaults and to the assemblies of the globals type.</summary>
    public IReadOnlyList<Assembly> References { get; init; } = [];

    /// <summary>Namespaces that every cell imports, in addition to the defaults.</summary>
    public IReadOnlyList<string> Imports { get; init; } = [];

    /// <summary>A check that runs before compile. Null means no check.</summary>
    public ICellValidator? Validator { get; init; }
}
