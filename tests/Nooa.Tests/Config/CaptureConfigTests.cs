// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unit/test_remaining_gaps.py

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class CaptureConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        var c = new CaptureConfig();

        Assert.Equal(50_000, c.MaxStdout);
        Assert.Equal(2_000, c.MaxStderr);
        Assert.Equal(10_000, c.MaxError);
        Assert.Null(c.Tail);
        Assert.False(c.FileBacked);
        c.Validate();
    }

    [Fact]
    public void Validate_NegativeMaxStdout_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new CaptureConfig { MaxStdout = -1 }.Validate());

        Assert.Equal("Invalid CaptureConfig:\n  - capture.MaxStdout must be > 0, got -1", ex.Message);
    }

    [Fact]
    public void Validate_ZeroMaxStderr_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new CaptureConfig { MaxStderr = 0 }.Validate());

        Assert.Contains("MaxStderr must be > 0", ex.Message);
    }

    [Fact]
    public void Validate_NegativeTail_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new CaptureConfig { Tail = -1 }.Validate());

        Assert.Contains("Tail must be >= 0", ex.Message);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1500)]
    public void Validate_TailNotBelowMaxStdout_Throws(int tail)
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new CaptureConfig { MaxStdout = 1000, Tail = tail }.Validate());

        Assert.Contains($"capture.Tail ({tail:N0}) must be less than capture.MaxStdout (1,000)", ex.Message);
    }

    [Fact]
    public void Validate_TailEqualToMaxStderr_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new CaptureConfig { MaxStdout = 50_000, MaxStderr = 1000, Tail = 1000 }.Validate());

        Assert.Contains("must be less than capture.MaxStderr", ex.Message);
    }

    [Fact]
    public void Validate_ValidTail_Passes()
    {
        var c = new CaptureConfig { MaxStdout = 50_000, MaxStderr = 20_000, Tail = 5000 };

        c.Validate();

        Assert.Equal(5000, c.Tail);
    }

    [Fact]
    public void Validate_SeveralErrors_AllReportedOnSeparateLines()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new CaptureConfig { MaxStdout = 0, MaxStderr = -1 }.Validate());

        Assert.StartsWith("Invalid CaptureConfig:\n  - ", ex.Message);
        Assert.Contains("MaxStdout", ex.Message);
        Assert.Contains("MaxStderr", ex.Message);
        Assert.Equal(3, ex.Message.Split('\n').Length);
    }
}
