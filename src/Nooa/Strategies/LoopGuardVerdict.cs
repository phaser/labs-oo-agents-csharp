// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/loop_guard.py

namespace Nooa.Strategies;

/// <summary>The decision of <see cref="LoopGuard"/> for one tool call.</summary>
/// <param name="Action">What to do.</param>
/// <param name="Repeats">The number of identical calls in the window, including this one.</param>
/// <param name="Outcome">The previous outcome of the call. Empty for a verdict that <see cref="LoopGuard.After"/> returns.</param>
internal sealed record LoopGuardVerdict(LoopGuardAction Action, int Repeats, string Outcome = "");
