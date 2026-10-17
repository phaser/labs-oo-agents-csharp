// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategy_validation.py

namespace Nooa.Strategies;

/// <summary>
/// A check that runs on a result that already has the right type. Throw <see cref="Nooa.Errors.InvariantException"/>
/// to reject the result, so the model can correct it.
/// </summary>
/// <param name="agent">The agent that runs the call.</param>
/// <param name="result">The validated result, or null when the strategy has no value.</param>
/// <param name="call">The call that was generated.</param>
public delegate void MethodPostcondition(Agent agent, object? result, CurrentCall call);
