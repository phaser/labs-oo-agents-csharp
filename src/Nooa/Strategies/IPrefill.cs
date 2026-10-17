// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/prefill.py

using Nooa.Config;

namespace Nooa.Strategies;

/// <summary>
/// Generates the code of a first cell that runs before the main CodeAct loop. The code runs through the normal cell path,
/// so the validator checks it and the model sees it as a synthetic tool call with its output.
/// </summary>
public interface IPrefill
{
    /// <summary>Creates the prefill code for a call.</summary>
    /// <param name="call">The call with its method details and arguments.</param>
    /// <param name="config">The truncation settings, for the limits of the printed inputs. Null uses the defaults.</param>
    /// <returns>The C# code to run, or null to skip the prefill.</returns>
    string? GetCode(CurrentCall call, TruncationConfig? config = null);
}
