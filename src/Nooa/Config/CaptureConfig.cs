// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/truncation_config.py

namespace Nooa.Config;

/// <summary>
/// Character budgets for raw text streams that the runtime captures.
/// </summary>
/// <remarks>
/// The budgets apply at capture time, when a cell writes to stdout or stderr or formats an exception.
/// The captured text is cut to the budget from the head and the tail, with a marker where text is removed.
/// Every field here counts characters.
/// </remarks>
public sealed record CaptureConfig : ConfigBase
{
    /// <summary>Maximum characters of stdout per cell.</summary>
    public int MaxStdout { get; init { field = value; MarkSet(); } } = 50_000;

    /// <summary>Maximum characters of stderr per cell.</summary>
    public int MaxStderr { get; init { field = value; MarkSet(); } } = 2_000;

    /// <summary>Maximum characters of error text per cell.</summary>
    public int MaxError { get; init { field = value; MarkSet(); } } = 10_000;

    /// <summary>
    /// Characters reserved for the tail window in head and tail truncation. Applies to stdout, stderr and error capture.
    /// Null means half of each stream budget (an even head and tail split).
    /// </summary>
    public int? Tail { get; init { field = value; MarkSet(); } }

    /// <summary>
    /// When true, stdout and stderr capture streams the full output to a temporary file on disk
    /// and still truncates the in-memory text. The truncation notice names the file.
    /// </summary>
    public bool FileBacked { get; init { field = value; MarkSet(); } }

    public override void Validate()
    {
        var errors = new List<string>();
        var budgets = new (string Name, int Value)[]
        {
            (nameof(MaxStdout), MaxStdout),
            (nameof(MaxStderr), MaxStderr),
            (nameof(MaxError), MaxError),
        };
        foreach (var (name, value) in budgets)
        {
            if (value <= 0)
            {
                errors.Add($"capture.{name} must be > 0, got {value}");
            }
        }

        if (Tail is { } tail)
        {
            if (tail < 0)
            {
                errors.Add($"capture.{nameof(Tail)} must be >= 0, got {tail}");
            }
            else
            {
                foreach (var (name, value) in budgets)
                {
                    if (tail >= value)
                    {
                        errors.Add($"capture.{nameof(Tail)} ({Group(tail)}) must be less than capture.{name} ({Group(value)})");
                    }
                }
            }
        }

        ThrowIfInvalid(nameof(CaptureConfig), errors);
    }
}
