// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/strategy_config.py

using Nooa.Strategies;

namespace Nooa.Config;

/// <summary>
/// Config for the Predict strategy.
/// </summary>
/// <remarks>
/// The condition lists are method-local checks (see <see cref="StrategyConditions"/>). As upstream, the config holds them
/// but <see cref="Nooa.Strategies.PredictStrategy"/> does not run them yet. CodeAct runs them (T17).
/// </remarks>
public sealed record PredictConfig : ConfigBase
{
    /// <summary>Checks that run before generation and fail fast.</summary>
    public IReadOnlyList<MethodPrecondition> Preconditions { get; init { field = value; MarkSet(); } } = [];

    /// <summary>Checks that run on a result of the right type. An <see cref="Nooa.Errors.InvariantException"/> asks the model to retry.</summary>
    public IReadOnlyList<MethodPostcondition> Postconditions { get; init { field = value; MarkSet(); } } = [];

    /// <summary>Maximum retries after a validation error.</summary>
    public int MaxRetries { get; init { field = value; MarkSet(); } } = 10;

    /// <summary>Maximum output tokens per model call. Null means the client default.</summary>
    public int? MaxTokens { get; init { field = value; MarkSet(); } }

    /// <summary>Sampling temperature. Null means the client default.</summary>
    public double? Temperature { get; init { field = value; MarkSet(); } }

    /// <summary>Nucleus sampling value. Null means the client default.</summary>
    public double? TopP { get; init { field = value; MarkSet(); } }

    /// <summary>Maximum characters of a validation error that goes back to the model.</summary>
    public int MaxErrorChars { get; init { field = value; MarkSet(); } } = 1000;

    /// <summary>Maximum characters of one parameter. Null disables the parameter-size guard.</summary>
    public int? MaxParamChars { get; init { field = value; MarkSet(); } } = 200_000;

    /// <summary>How the output goes back into the conversation history.</summary>
    public PredictOutputSerialization OutputSerialization { get; init { field = value; MarkSet(); } } = PredictOutputSerialization.Event;

    /// <summary>Throws <see cref="Nooa.Errors.ConfigValidationException"/> when a condition list is null or holds a null entry.</summary>
    public override void Validate()
    {
        var errors = new List<string>();
        if (Preconditions is null || Preconditions.Any(c => c is null))
        {
            errors.Add("Preconditions must be a list of non-null conditions");
        }

        if (Postconditions is null || Postconditions.Any(c => c is null))
        {
            errors.Add("Postconditions must be a list of non-null conditions");
        }

        ThrowIfInvalid(nameof(PredictConfig), errors);
    }

    /// <summary>Returns a copy of this config with every field that <paramref name="other"/> sets explicitly.</summary>
    public PredictConfig MergeWith(PredictConfig other)
    {
        RequireFieldsSet(other);
        return CopySetFields(this with { }, other);
    }
}
