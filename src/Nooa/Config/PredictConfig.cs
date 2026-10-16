// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/strategy_config.py

namespace Nooa.Config;

/// <summary>
/// Config for the Predict strategy.
/// </summary>
/// <remarks>
/// The precondition and postcondition fields are not ported yet. They arrive with T14 (see <c>work/t14-template-predict.md</c>).
/// </remarks>
public sealed record PredictConfig : ConfigBase
{
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

    /// <summary>Returns a copy of this config with every field that <paramref name="other"/> sets explicitly.</summary>
    public PredictConfig MergeWith(PredictConfig other)
    {
        RequireFieldsSet(other);
        return CopySetFields(this with { }, other);
    }
}
