// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategy_validation.py

using Nooa.Errors;

namespace Nooa.Strategies;

/// <summary>
/// Runs the method preconditions and postconditions of a strategy config.
/// </summary>
/// <remarks>
/// Upstream <c>normalize_conditions</c> checks that every entry is callable. A delegate list needs only a null check,
/// which <see cref="Nooa.Config.PredictConfig.Validate"/> does.
/// </remarks>
public static class StrategyConditions
{
    /// <summary>Runs the preconditions in order. The first failure stops the run.</summary>
    /// <exception cref="InvariantException">A precondition rejects the call.</exception>
    /// <exception cref="NooaRuntimeException">A precondition throws any other exception.</exception>
    public static void RunPreconditions(Agent agent, CurrentCall call, IEnumerable<MethodPrecondition> preconditions)
    {
        ArgumentNullException.ThrowIfNull(preconditions);
        foreach (var precondition in preconditions)
        {
            Run("precondition", precondition, () => precondition(agent, call));
        }
    }

    /// <summary>Runs the postconditions in order. The first failure stops the run.</summary>
    /// <exception cref="InvariantException">A postcondition rejects the result.</exception>
    /// <exception cref="NooaRuntimeException">A postcondition throws any other exception.</exception>
    public static void RunPostconditions(Agent agent, object? result, CurrentCall call, IEnumerable<MethodPostcondition> postconditions)
    {
        ArgumentNullException.ThrowIfNull(postconditions);
        foreach (var postcondition in postconditions)
        {
            Run("postcondition", postcondition, () => postcondition(agent, result, call));
        }
    }

    private static void Run(string kind, Delegate condition, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is not (InvariantException or OperationCanceledException))
        {
            throw new NooaRuntimeException($"method {kind} '{condition.Method.Name}' failed", e);
        }
    }
}
