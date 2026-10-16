// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/config/test_execution_config.py

using Nooa.Config;

namespace Nooa.Tests.Config;

public class ExecutionConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        Assert.Equal(10, new ExecutionConfig().MaxNestingDepth);
    }

    [Fact]
    public void Properties_AreInitOnly()
    {
        var setter = typeof(ExecutionConfig).GetProperty(nameof(ExecutionConfig.MaxNestingDepth))!.SetMethod!;

        Assert.Contains(setter.ReturnParameter.GetRequiredCustomModifiers(), t => t.Name == "IsExternalInit");
    }

    [Fact]
    public void MergeWith_ExplicitField_OverridesOnlyThatField()
    {
        var merged = new ExecutionConfig().MergeWith(new ExecutionConfig { MaxNestingDepth = 5 });

        Assert.Equal(5, merged.MaxNestingDepth);
    }

    [Fact]
    public void MergeWith_ExplicitDefaultValue_StillOverrides()
    {
        var merged = new ExecutionConfig { MaxNestingDepth = 3 }.MergeWith(new ExecutionConfig { MaxNestingDepth = 10 });

        Assert.Equal(10, merged.MaxNestingDepth);
    }

    [Fact]
    public void MergeWith_EmptyFieldsSet_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ExecutionConfig().MergeWith(new ExecutionConfig()));

        Assert.Contains("MergeWith() received a config with no fields set", ex.Message);
    }

    [Fact]
    public void MergeWith_ReturnsNewInstanceAndKeepsBase()
    {
        var baseConfig = new ExecutionConfig { MaxNestingDepth = 2 };

        var merged = baseConfig.MergeWith(new ExecutionConfig { MaxNestingDepth = 5 });

        Assert.NotSame(baseConfig, merged);
        Assert.Equal(2, baseConfig.MaxNestingDepth);
    }

    [Fact]
    public void MergeWith_Result_KeepsBaseFieldsSetAndAddsOthers()
    {
        var merged = new ExecutionConfig { MaxNestingDepth = 2 }.MergeWith(new ExecutionConfig { MaxNestingDepth = 5 });

        Assert.True(merged.IsSet(nameof(ExecutionConfig.MaxNestingDepth)));
    }
}
