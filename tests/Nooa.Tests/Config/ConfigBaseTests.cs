// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class ConfigBaseTests
{
    [Fact]
    public void PropertyInitializer_DefaultValue_IsNotMarkedSet()
    {
        var config = new ExecutionConfig();

        Assert.Empty(config.FieldsSet);
        Assert.False(config.IsSet(nameof(ExecutionConfig.MaxNestingDepth)));
    }

    [Fact]
    public void ObjectInitializer_ExplicitDefaultValue_IsMarkedSet()
    {
        var config = new ExecutionConfig { MaxNestingDepth = 10 };

        Assert.True(config.IsSet(nameof(ExecutionConfig.MaxNestingDepth)));
        Assert.Equal([nameof(ExecutionConfig.MaxNestingDepth)], config.FieldsSet);
    }

    [Fact]
    public void ObjectInitializer_ExplicitNull_IsMarkedSet()
    {
        var config = new CodeActConfig { MaxIterations = null };

        Assert.True(config.IsSet(nameof(CodeActConfig.MaxIterations)));
        Assert.Null(config.MaxIterations);
    }

    [Fact]
    public void With_Copy_HasItsOwnFieldsSet()
    {
        var original = new CodeActConfig { MaxRetries = 1 };

        var copy = original with { Temperature = 0.5 };

        Assert.True(copy.IsSet(nameof(CodeActConfig.MaxRetries)));
        Assert.True(copy.IsSet(nameof(CodeActConfig.Temperature)));
        Assert.False(original.IsSet(nameof(CodeActConfig.Temperature)));
    }

    [Fact]
    public void With_EmptyCopy_KeepsSetFieldsAndAddsNone()
    {
        var original = new CodeActConfig { MaxRetries = 1 };

        var copy = original with { };

        Assert.Equal(original.FieldsSet, copy.FieldsSet);
        Assert.NotSame(original.FieldsSet, copy.FieldsSet);
    }

    [Fact]
    public void Equals_SameValuesDifferentFieldsSet_AreEqual()
    {
        var implicitDefault = new ExecutionConfig();
        var explicitDefault = new ExecutionConfig { MaxNestingDepth = 10 };

        Assert.Equal(implicitDefault, explicitDefault);
        Assert.Equal(implicitDefault.GetHashCode(), explicitDefault.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(new ExecutionConfig { MaxNestingDepth = 3 }, new ExecutionConfig { MaxNestingDepth = 4 });
    }

    [Fact]
    public void Equals_DifferentRecordTypes_AreNotEqual()
    {
        ConfigBase a = new ExecutionConfig();
        ConfigBase b = new MediaCaptureConfig();

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Validate_DefaultImplementation_AcceptsAnyValue()
    {
        new ExecutionConfig { MaxNestingDepth = -1 }.Validate();
    }

    [Fact]
    public void MergeWith_EmptyOther_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ExecutionConfig().MergeWith(new ExecutionConfig()));

        Assert.Contains("MergeWith() received a config with no fields set", ex.Message);
        Assert.Contains("ExecutionConfig", ex.Message);
    }

    [Fact]
    public void ConfigValidationException_IsNooaException()
    {
        Assert.IsAssignableFrom<NooaException>(new ConfigValidationException("x"));
    }
}
