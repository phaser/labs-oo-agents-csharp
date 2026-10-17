// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py (execute_code)

namespace Nooa.Runtime;

/// <summary>
/// Runs the code cells that a model writes. Variables persist from one cell to the next.
/// </summary>
/// <remarks>
/// An engine runs model-written code. It is a guardrail and not a security boundary (D12).
/// </remarks>
public interface IExecutionEngine
{
    /// <summary>The number of the last cell. Every call to <see cref="ExecuteAsync"/> uses the next number.</summary>
    int ExecutionCount { get; }

    /// <summary>The live values of earlier cells, for <c>Out[n]</c>.</summary>
    OutAccessor Out { get; }

    /// <summary>
    /// True after a cell ignored its time limit and the engine gave it up. The session state is not safe then,
    /// and every later call throws <see cref="InvalidOperationException"/>. The caller must end its work.
    /// </summary>
    bool IsAbandoned { get; }

    /// <summary>Reads the current value of a variable that an earlier cell or <see cref="DeclareArgumentsAsync"/> declared.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value, when the variable exists.</param>
    /// <returns>True when a variable with this name exists.</returns>
    bool TryGetVariable(string name, out object? value);

    /// <summary>Declares the arguments of the current agent method as typed variables for the next cells.</summary>
    /// <exception cref="InvalidOperationException">An argument name or type cannot be declared.</exception>
    Task DeclareArgumentsAsync(IReadOnlyList<ReplArgument> arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one cell. A compile error, an exception, a timeout and a cancel come back in the result and do not throw.
    /// </summary>
    /// <param name="code">The code. Markdown fences around it are removed.</param>
    /// <param name="cancellationToken">Cancels the cell. The cell stops only when it observes the token.</param>
    Task<CellResult> ExecuteAsync(string code, CancellationToken cancellationToken = default);
}
