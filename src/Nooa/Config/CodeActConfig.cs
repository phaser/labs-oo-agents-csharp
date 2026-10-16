// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/strategy_config.py

namespace Nooa.Config;

/// <summary>
/// Config for the CodeAct strategy.
/// </summary>
/// <remarks>
/// The restrictions, sandbox, prefill, precondition and postcondition fields are not ported yet.
/// They arrive with the tasks that port the types they use (see <c>work/t17-codeact.md</c>).
/// </remarks>
public sealed record CodeActConfig : ConfigBase
{
    /// <summary>Maximum generation iterations. Null means unlimited.</summary>
    public int? MaxIterations { get; init { field = value; MarkSet(); } }

    /// <summary>Maximum retries after a correctable error.</summary>
    public int MaxRetries { get; init { field = value; MarkSet(); } } = 3;

    /// <summary>
    /// Maximum consecutive turns where the model returns plain text instead of a tool call before the run aborts.
    /// A real tool call resets the counter. 0 disables the guard.
    /// </summary>
    public int MaxConsecutiveTextOnly { get; init { field = value; MarkSet(); } } = 3;

    /// <summary>Time limit for one cell. Null means no limit.</summary>
    public TimeSpan? CellTimeout { get; init { field = value; MarkSet(); } }

    /// <summary>Maximum output tokens per model call. Null means the client default.</summary>
    public int? MaxTokens { get; init { field = value; MarkSet(); } }

    /// <summary>Sampling temperature. Null means the client default.</summary>
    public double? Temperature { get; init { field = value; MarkSet(); } }

    /// <summary>Nucleus sampling value. Null means the client default.</summary>
    public double? TopP { get; init { field = value; MarkSet(); } }

    /// <summary>Maximum tool calls per run. Null means unlimited.</summary>
    public int? MaxToolCalls { get; init { field = value; MarkSet(); } }

    /// <summary>When true, the strategy translates tool calls between provider formats.</summary>
    public bool TranslateToolCalls { get; init { field = value; MarkSet(); } }

    /// <summary>Opt-in detection of repeated identical tool calls. Null disables it.</summary>
    public LoopGuardConfig? LoopGuard { get; init { field = value; MarkSet(); } }

    /// <summary>Validates the loop-guard config when it is present.</summary>
    public override void Validate() => LoopGuard?.Validate();

    /// <summary>Returns a copy of this config with every field that <paramref name="other"/> sets explicitly.</summary>
    public CodeActConfig MergeWith(CodeActConfig other)
    {
        RequireFieldsSet(other);
        return CopySetFields(this with { }, other);
    }
}
