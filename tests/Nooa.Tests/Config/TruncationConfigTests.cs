// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_truncation_config.py

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class TruncationConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        var config = new TruncationConfig();

        Assert.Null(config.MaxContextTokens);
        Assert.Null(config.MaxEventTokens);
        Assert.Equal(5, config.MinPreservedEvents);
        Assert.Equal(4_096, config.ResponseReserveTokens);
        Assert.Equal(50_000, config.Capture.MaxStdout);
        Assert.Equal(2_000, config.Capture.MaxStderr);
        Assert.Equal(10_000, config.Capture.MaxError);
        Assert.Equal(5, config.MediaCapture.MaxAttachmentsPerExecution);
        Assert.Equal(200, config.EventFormat.MaxLength);
        Assert.Equal(10_000, config.EventFormat.MaxString);
        Assert.Equal(5, config.EventFormat.MaxDepth);
        Assert.Equal(25, config.PrefillFormat.MaxLength);
        Assert.Equal(2_000, config.PrefillFormat.MaxString);
        Assert.Equal(4, config.PrefillFormat.MaxDepth);
        Assert.Null(config.ContextBlockFormat.MaxLength);
        Assert.Null(config.ContextBlockFormat.MaxString);
        Assert.Null(config.ContextBlockFormat.MaxDepth);
        Assert.Empty(config.FieldsSet);
    }

    [Fact]
    public void Default_Instance_HasDefaults()
    {
        Assert.Equal(50_000, TruncationConfig.Default.Capture.MaxStdout);
        Assert.Equal(200, TruncationConfig.Default.EventFormat.MaxLength);
        Assert.Null(TruncationConfig.Default.ContextBlockFormat.MaxLength);
        Assert.Empty(TruncationConfig.Default.FieldsSet);
    }

    [Fact]
    public void CustomValues_OverrideDefaults()
    {
        var config = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 100_000, MaxStderr = 30_000 },
            MediaCapture = new MediaCaptureConfig { MaxAttachmentsPerExecution = 20 },
            EventFormat = new FormatConfig { MaxLength = 100, MaxString = 1000, MaxDepth = 5 },
        };

        Assert.Equal(100_000, config.Capture.MaxStdout);
        Assert.Equal(30_000, config.Capture.MaxStderr);
        Assert.Equal(20, config.MediaCapture.MaxAttachmentsPerExecution);
        Assert.Equal(100, config.EventFormat.MaxLength);
    }

    [Fact]
    public void Equals_SameValues_AreEqual()
    {
        var a = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 } };
        var b = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 } };

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentValues_AreNotEqual()
    {
        var a = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 } };
        var b = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 50_000 } };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void MergeWith_Null_ReturnsSelf()
    {
        var config = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 } };

        Assert.Same(config, config.MergeWith(null));
    }

    [Fact]
    public void MergeWith_EmptyFieldsSet_Throws()
    {
        var baseConfig = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000, MaxStderr = 40_000 } };

        var ex = Assert.Throws<ArgumentException>(() => baseConfig.MergeWith(new TruncationConfig()));

        Assert.Contains("MergeWith", ex.Message);
    }

    [Fact]
    public void MergeWith_Overrides_UseExplicitValues()
    {
        var baseConfig = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 50_000 },
            EventFormat = new FormatConfig { MaxLength = 50 },
        };
        var overrides = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 100_000 },
            EventFormat = new FormatConfig { MaxLength = 100 },
        };

        var merged = baseConfig.MergeWith(overrides);

        Assert.Equal(100_000, merged.Capture.MaxStdout);
        Assert.Equal(100, merged.EventFormat.MaxLength);
    }

    [Fact]
    public void MergeWith_ReturnsNewInstance()
    {
        var a = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 } };
        var b = new TruncationConfig { Capture = new CaptureConfig { MaxStderr = 30_000 } };

        var merged = a.MergeWith(b);

        Assert.NotSame(a, merged);
        Assert.NotSame(b, merged);
    }

    [Fact]
    public void MergeWith_ThreeWay_KeepsEarlierAndAppliesLater()
    {
        var classLevel = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 } };
        var instanceLevel = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 100_000 },
            EventFormat = new FormatConfig { MaxLength = 100 },
        };
        var methodLevel = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 100_000 },
            EventFormat = new FormatConfig { MaxLength = 100, MaxDepth = 10 },
        };

        var merged = classLevel.MergeWith(instanceLevel).MergeWith(methodLevel);

        Assert.Equal(100_000, merged.Capture.MaxStdout);
        Assert.Equal(100, merged.EventFormat.MaxLength);
        Assert.Equal(10, merged.EventFormat.MaxDepth);
    }

    [Fact]
    public void MergeWith_LaterOverridesEarlier()
    {
        var first = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000 }, EventFormat = new FormatConfig { MaxLength = 100 } };
        var second = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 200_000 }, EventFormat = new FormatConfig { MaxLength = 100 } };

        var merged = first.MergeWith(second);

        Assert.Equal(200_000, merged.Capture.MaxStdout);
        Assert.Equal(100, merged.EventFormat.MaxLength);
    }

    [Fact]
    public void MergeWith_PartialOverride_ChangesOnlySpecifiedSubConfig()
    {
        var baseConfig = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 100_000, MaxStderr = 30_000 },
            EventFormat = new FormatConfig { MaxLength = 100 },
        };
        var overrides = new TruncationConfig { EventFormat = new FormatConfig { MaxLength = 200 } };

        var merged = baseConfig.MergeWith(overrides);

        Assert.Equal(100_000, merged.Capture.MaxStdout);
        Assert.Equal(30_000, merged.Capture.MaxStderr);
        Assert.Equal(200, merged.EventFormat.MaxLength);
    }

    [Fact]
    public void MergeWith_PartialOverrideWithinSubConfig_KeepsOtherSubConfigFields()
    {
        var baseConfig = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 100_000, MaxStderr = 40_000, MaxError = 20_000 } };
        var overrides = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 200_000 } };

        var merged = baseConfig.MergeWith(overrides);

        Assert.Equal(200_000, merged.Capture.MaxStdout);
        Assert.Equal(40_000, merged.Capture.MaxStderr);
        Assert.Equal(20_000, merged.Capture.MaxError);
    }

    [Fact]
    public void MergeWith_ContextBlockFormat_IsIndependentOfOtherFormats()
    {
        var overrides = new TruncationConfig { ContextBlockFormat = new FormatConfig { MaxString = 50_000 } };

        var merged = new TruncationConfig().MergeWith(overrides);

        Assert.Equal(50_000, merged.ContextBlockFormat.MaxString);
        Assert.Null(merged.ContextBlockFormat.MaxLength);
        Assert.Null(merged.ContextBlockFormat.MaxDepth);
        Assert.Equal(10_000, merged.EventFormat.MaxString);
        Assert.Equal(2_000, merged.PrefillFormat.MaxString);
    }

    [Fact]
    public void MergeWith_MediaCapture_IsIndependentOfTextCapture()
    {
        var baseConfig = new TruncationConfig
        {
            Capture = new CaptureConfig { MaxStdout = 100_000, MaxStderr = 30_000 },
            MediaCapture = new MediaCaptureConfig { MaxAttachmentsPerExecution = 5 },
        };
        var overrides = new TruncationConfig { MediaCapture = new MediaCaptureConfig { MaxAttachmentsPerExecution = 20 } };

        var merged = baseConfig.MergeWith(overrides);

        Assert.Equal(20, merged.MediaCapture.MaxAttachmentsPerExecution);
        Assert.Equal(100_000, merged.Capture.MaxStdout);
        Assert.Equal(30_000, merged.Capture.MaxStderr);
    }

    [Fact]
    public void MergeWith_ExplicitNull_OverridesBaseValue()
    {
        var baseConfig = new TruncationConfig { EventFormat = new FormatConfig { MaxLength = 100 } };
        var overrides = new TruncationConfig { EventFormat = new FormatConfig { MaxLength = null } };

        var merged = baseConfig.MergeWith(overrides);

        Assert.Null(merged.EventFormat.MaxLength);
    }

    [Fact]
    public void MergeWith_UnsetFields_DoNotOverride()
    {
        var baseConfig = new TruncationConfig
        {
            EventFormat = new FormatConfig { MaxLength = 100 },
            Capture = new CaptureConfig { MaxStdout = 80_000 },
        };
        var overrides = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 90_000 } };

        var merged = baseConfig.MergeWith(overrides);

        Assert.Equal(100, merged.EventFormat.MaxLength);
        Assert.Equal(90_000, merged.Capture.MaxStdout);
    }

    [Fact]
    public void MergeWith_TopLevelExplicitValue_Overrides()
    {
        var merged = new TruncationConfig { MaxContextTokens = 10_000 }.MergeWith(new TruncationConfig { MaxContextTokens = 20_000 });

        Assert.Equal(20_000, merged.MaxContextTokens);
    }

    [Fact]
    public void MergeWith_ExplicitNullTopLevel_ClearsBaseValue()
    {
        var merged = new TruncationConfig { MaxContextTokens = 10_000 }.MergeWith(new TruncationConfig { MaxContextTokens = null });

        Assert.Null(merged.MaxContextTokens);
    }

    [Fact]
    public void MergeWith_SubConfigNotSetByOther_IsNotMarkedSetOnResult()
    {
        var merged = new TruncationConfig().MergeWith(new TruncationConfig { MaxContextTokens = 5 });

        Assert.True(merged.IsSet(nameof(TruncationConfig.MaxContextTokens)));
        Assert.False(merged.IsSet(nameof(TruncationConfig.Capture)));
    }

    [Fact]
    public void MergeWith_DoesNotChangeBaseOrOtherSubConfigs()
    {
        var baseCapture = new CaptureConfig { MaxStdout = 100_000 };
        var baseConfig = new TruncationConfig { Capture = baseCapture };

        baseConfig.MergeWith(new TruncationConfig { Capture = new CaptureConfig { MaxStderr = 5 } });

        Assert.Equal(2_000, baseConfig.Capture.MaxStderr);
        Assert.Equal([nameof(CaptureConfig.MaxStdout)], baseCapture.FieldsSet);
    }

    [Fact]
    public void Validate_Defaults_Passes()
    {
        new TruncationConfig().Validate();
    }

    [Fact]
    public void Validate_ZeroMaxContextTokens_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new TruncationConfig { MaxContextTokens = 0 }.Validate());

        Assert.Equal("Invalid TruncationConfig:\n  - MaxContextTokens must be > 0 or null, got 0", ex.Message);
    }

    [Fact]
    public void Validate_NegativeMaxEventTokens_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new TruncationConfig { MaxEventTokens = -5 }.Validate());

        Assert.Contains("MaxEventTokens must be > 0 or null", ex.Message);
    }

    [Fact]
    public void Validate_NullMaxContextTokens_Passes()
    {
        var config = new TruncationConfig { MaxContextTokens = null };

        config.Validate();

        Assert.Null(config.MaxContextTokens);
    }

    [Fact]
    public void Validate_InvalidSubConfig_Throws()
    {
        var config = new TruncationConfig { Capture = new CaptureConfig { MaxStdout = 0 } };

        var ex = Assert.Throws<ConfigValidationException>(config.Validate);

        Assert.StartsWith("Invalid CaptureConfig:", ex.Message);
    }
}
