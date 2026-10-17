// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/config/test_strategy_configs.py

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class PredictConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        var c = new PredictConfig();

        Assert.Equal(10, c.MaxRetries);
        Assert.Null(c.MaxTokens);
        Assert.Null(c.Temperature);
        Assert.Null(c.TopP);
        Assert.Equal(1000, c.MaxErrorChars);
        Assert.Equal(200_000, c.MaxParamChars);
        Assert.Equal(PredictOutputSerialization.Event, c.OutputSerialization);
    }

    [Fact]
    public void MergeWith_ExplicitField_OverridesOnlyThatField()
    {
        var merged = new PredictConfig().MergeWith(new PredictConfig { MaxRetries = 5 });

        Assert.Equal(5, merged.MaxRetries);
        Assert.Equal(1000, merged.MaxErrorChars);
    }

    [Fact]
    public void MergeWith_ExplicitNullMaxParamChars_DisablesGuard()
    {
        var merged = new PredictConfig().MergeWith(new PredictConfig { MaxParamChars = null });

        Assert.Null(merged.MaxParamChars);
    }

    [Fact]
    public void MergeWith_OutputSerialization_Overrides()
    {
        var merged = new PredictConfig().MergeWith(new PredictConfig { OutputSerialization = PredictOutputSerialization.ToolCall });

        Assert.Equal(PredictOutputSerialization.ToolCall, merged.OutputSerialization);
    }

    [Fact]
    public void MergeWith_EmptyFieldsSet_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PredictConfig().MergeWith(new PredictConfig()));

        Assert.Contains("MergeWith() received a config with no fields set", ex.Message);
    }

    [Fact]
    public void Defaults_Conditions_AreEmpty()
    {
        var c = new PredictConfig();

        Assert.Empty(c.Preconditions);
        Assert.Empty(c.Postconditions);
    }

    [Fact]
    public void MergeWith_Conditions_Override()
    {
        static void Check(Nooa.Agent agent, object? result, Nooa.Strategies.CurrentCall call)
        {
        }

        var merged = new PredictConfig().MergeWith(new PredictConfig { Postconditions = [Check] });

        Assert.Single(merged.Postconditions);
        Assert.Empty(merged.Preconditions);
    }

    [Fact]
    public void Validate_NullCondition_Throws()
    {
        var error = Assert.Throws<ConfigValidationException>(() => new PredictConfig { Postconditions = [null!] }.Validate());

        Assert.Contains("Postconditions", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Defaults_Passes()
    {
        new PredictConfig().Validate();
    }
}
