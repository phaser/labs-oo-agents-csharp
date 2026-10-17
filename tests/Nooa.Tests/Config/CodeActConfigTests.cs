// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/config/test_strategy_configs.py

using Nooa.Config;
using Nooa.Errors;
using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Config;

public class CodeActConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        var c = new CodeActConfig();

        Assert.Null(c.MaxIterations);
        Assert.Equal(3, c.MaxRetries);
        Assert.Equal(3, c.MaxConsecutiveTextOnly);
        Assert.Null(c.CellTimeout);
        Assert.Null(c.MaxTokens);
        Assert.Null(c.Temperature);
        Assert.Null(c.TopP);
        Assert.Null(c.MaxToolCalls);
        Assert.False(c.TranslateToolCalls);
        Assert.Null(c.LoopGuard);
        Assert.Equal(RestrictedApiGroup.None, c.AllowedApis);
        Assert.IsType<InspectInputsPrefill>(c.Prefill);
        Assert.Empty(c.Preconditions);
        Assert.Empty(c.Postconditions);
    }

    [Fact]
    public void MergeWith_ExplicitNullPrefillAndConditions_Overrides()
    {
        MethodPrecondition check = (_, _) => { };
        var merged = new CodeActConfig().MergeWith(new CodeActConfig
        {
            Prefill = null,
            Preconditions = [check],
            AllowedApis = RestrictedApiGroup.Network,
        });

        Assert.Null(merged.Prefill);
        Assert.Same(check, Assert.Single(merged.Preconditions));
        Assert.Equal(RestrictedApiGroup.Network, merged.AllowedApis);
        Assert.Empty(merged.Postconditions);
    }

    [Fact]
    public void Validate_NullCondition_Throws()
    {
        var c = new CodeActConfig { Postconditions = [null!] };

        var e = Assert.Throws<ConfigValidationException>(c.Validate);

        Assert.Contains("Postconditions must be a list of non-null conditions", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeWith_ExplicitFields_OverrideOnlyThose()
    {
        var merged = new CodeActConfig().MergeWith(new CodeActConfig { MaxIterations = 5, Temperature = 0.7 });

        Assert.Equal(5, merged.MaxIterations);
        Assert.Equal(0.7, merged.Temperature);
        Assert.Equal(3, merged.MaxRetries);
    }

    [Fact]
    public void MergeWith_ExplicitNullMaxIterations_ClearsLimit()
    {
        var merged = new CodeActConfig { MaxIterations = 5 }.MergeWith(new CodeActConfig { MaxIterations = null });

        Assert.Null(merged.MaxIterations);
    }

    [Fact]
    public void MergeWith_LoopGuard_ReplacesWholeSubConfig()
    {
        var merged = new CodeActConfig { LoopGuard = new LoopGuardConfig { Window = 20 } }
            .MergeWith(new CodeActConfig { LoopGuard = new LoopGuardConfig { RepeatThreshold = 3 } });

        Assert.Equal(3, merged.LoopGuard!.RepeatThreshold);
        Assert.Equal(15, merged.LoopGuard.Window);
    }

    [Fact]
    public void MergeWith_EmptyFieldsSet_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CodeActConfig().MergeWith(new CodeActConfig()));

        Assert.Contains("MergeWith() received a config with no fields set", ex.Message);
    }

    [Fact]
    public void Validate_InvalidLoopGuard_Throws()
    {
        var config = new CodeActConfig { LoopGuard = new LoopGuardConfig { Window = 1 } };

        Assert.Throws<ConfigValidationException>(config.Validate);
    }

    [Fact]
    public void Validate_NoLoopGuard_Passes()
    {
        new CodeActConfig().Validate();
    }
}
