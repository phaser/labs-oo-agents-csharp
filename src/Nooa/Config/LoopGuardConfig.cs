// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/strategy_config.py

namespace Nooa.Config;

/// <summary>
/// Deterministic detection of a CodeAct loop that repeats one tool call.
/// </summary>
/// <remarks>
/// <para>
/// An action is a tool name plus its normalized arguments. For a code cell, the parsed code is used, so comments
/// and formatting do not matter. Its outcome is the error or the complete output it produced.
/// </para>
/// <list type="bullet">
/// <item>
/// A failing action is not run again when its last <c>RepeatThreshold - 1</c> runs in the window failed with an identical outcome.
/// The model receives a fixed loop-guard message instead.
/// </item>
/// <item>
/// A succeeding action that produced identical output <c>RepeatThreshold</c> times in the window runs,
/// and then the model receives the message.
/// </item>
/// <item>
/// A repeat of that action (with identical output, if it succeeded) within <c>Window</c> tool calls after the message
/// stops generation with <see cref="Nooa.Errors.LoopDetectedException"/>.
/// </item>
/// </list>
/// </remarks>
public sealed record LoopGuardConfig : ConfigBase
{
    /// <summary>Number of identical outcomes that triggers the guard. At least 2.</summary>
    public int RepeatThreshold { get; init { field = value; MarkSet(); } } = 7;

    /// <summary>Number of most recent tool calls, including the current one, that are compared. At least 2.</summary>
    public int Window { get; init { field = value; MarkSet(); } } = 15;

    /// <summary>Bound on the previous outcome that the loop-guard message quotes. At least 0.</summary>
    public int MaxOutcomeChars { get; init { field = value; MarkSet(); } } = 500;

    public override void Validate()
    {
        var errors = new List<string>();
        if (RepeatThreshold < 2)
        {
            errors.Add($"{nameof(RepeatThreshold)} must be >= 2, got {RepeatThreshold}");
        }

        if (Window < 2)
        {
            errors.Add($"{nameof(Window)} must be >= 2, got {Window}");
        }

        if (MaxOutcomeChars < 0)
        {
            errors.Add($"{nameof(MaxOutcomeChars)} must be >= 0, got {MaxOutcomeChars}");
        }

        ThrowIfInvalid(nameof(LoopGuardConfig), errors);
    }
}
