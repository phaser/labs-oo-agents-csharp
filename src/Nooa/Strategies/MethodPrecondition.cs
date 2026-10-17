// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategy_validation.py

namespace Nooa.Strategies;

/// <summary>
/// A check that runs before generation. Throw <see cref="Nooa.Errors.InvariantException"/> to reject the call.
/// </summary>
/// <param name="agent">The agent that runs the call.</param>
/// <param name="call">The call that is about to be generated.</param>
public delegate void MethodPrecondition(Agent agent, CurrentCall call);
