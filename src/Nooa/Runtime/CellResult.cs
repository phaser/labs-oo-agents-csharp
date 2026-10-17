// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py

using Nooa.ContextBlocks;

namespace Nooa.Runtime;

/// <summary>The outcome of one cell. Upstream names the same data <c>ExecutionResult</c>.</summary>
/// <param name="ExecutionCount">The cell number, shown to the model as <c>Cell In[N]</c> and used for <c>Out[N]</c>.</param>
/// <param name="Status"><see cref="ResultStatus.Complete"/>, <see cref="ResultStatus.Error"/> or <see cref="ResultStatus.Cancelled"/>.</param>
/// <param name="Stdout">The captured standard output, cut to the capture budget. Partial when the cell failed or stopped.</param>
/// <param name="Stderr">The captured standard error, cut to the capture budget.</param>
/// <param name="Error">The formatted error for the model. Empty when the cell completed.</param>
/// <param name="Value">The value of the last expression or the argument of <c>ReturnResult</c>. Null when there is none.</param>
/// <param name="ExplicitReturn">True when <paramref name="Value"/> comes from <c>ReturnResult</c> or a top-level <c>return</c>.</param>
public sealed record CellResult(
    int ExecutionCount,
    ResultStatus Status,
    string Stdout = "",
    string Stderr = "",
    string Error = "",
    object? Value = null,
    bool ExplicitReturn = false);
