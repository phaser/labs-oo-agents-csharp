// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/loop_guard.py

namespace Nooa.Strategies;

/// <summary>What the loop guard decides for a tool call.</summary>
internal enum LoopGuardAction
{
    /// <summary>Do not run the call: it failed with the same outcome before.</summary>
    Block,

    /// <summary>The call ran and produced the same output again. Warn the model.</summary>
    Nudge,

    /// <summary>The model repeated a call after a warning. End the generation.</summary>
    Stop,
}
