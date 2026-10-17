// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/strategy_config.py

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Config;

/// <summary>
/// Config for the CodeAct strategy.
/// </summary>
/// <remarks>
/// The sandbox and execution-backend fields are not ported yet. They arrive with the process sandbox (backlog item B01).
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

    /// <summary>
    /// The groups of restricted APIs that cells may use. The default is none, so the code validator refuses all of them.
    /// Upstream names this field <c>restrictions</c>.
    /// </summary>
    public RestrictedApiGroup AllowedApis { get; init { field = value; MarkSet(); } } = RestrictedApiGroup.None;

    /// <summary>
    /// The prefill that runs before the first model call. It shows the inputs as a first cell. The default is
    /// <see cref="InspectInputsPrefill"/>. Null turns the prefill off.
    /// </summary>
    public IPrefill? Prefill { get; init { field = value; MarkSet(); } } = new InspectInputsPrefill();

    /// <summary>Checks that run before generation and fail fast.</summary>
    public IReadOnlyList<MethodPrecondition> Preconditions { get; init { field = value; MarkSet(); } } = [];

    /// <summary>Checks that run on a result of the right type. An <see cref="Nooa.Errors.InvariantException"/> asks the model to retry.</summary>
    public IReadOnlyList<MethodPostcondition> Postconditions { get; init { field = value; MarkSet(); } } = [];

    /// <summary>Validates the loop-guard config when it is present, and the condition lists.</summary>
    public override void Validate()
    {
        LoopGuard?.Validate();
        var errors = new List<string>();
        if (Preconditions is null || Preconditions.Any(c => c is null))
        {
            errors.Add("Preconditions must be a list of non-null conditions");
        }

        if (Postconditions is null || Postconditions.Any(c => c is null))
        {
            errors.Add("Postconditions must be a list of non-null conditions");
        }

        ThrowIfInvalid(nameof(CodeActConfig), errors);
    }

    /// <summary>Returns a copy of this config with every field that <paramref name="other"/> sets explicitly.</summary>
    public CodeActConfig MergeWith(CodeActConfig other)
    {
        RequireFieldsSet(other);
        return CopySetFields(this with { }, other);
    }
}
