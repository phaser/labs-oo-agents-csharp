// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unit/test_remaining_gaps.py

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class FormatConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        var c = new FormatConfig();

        Assert.Equal(500, c.MaxString);
        Assert.Equal(50, c.MaxLength);
        Assert.Equal(4, c.MaxDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxLengthNotPositive_Throws(int value)
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new FormatConfig { MaxLength = value }.Validate());

        Assert.Contains("MaxLength must be > 0 or null", ex.Message);
    }

    [Fact]
    public void Validate_NegativeMaxString_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new FormatConfig { MaxString = -1 }.Validate());

        Assert.Contains("MaxString must be > 0 or null", ex.Message);
    }

    [Fact]
    public void Validate_ZeroMaxDepth_Throws()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new FormatConfig { MaxDepth = 0 }.Validate());

        Assert.Contains("MaxDepth must be > 0 or null", ex.Message);
    }

    [Fact]
    public void Validate_NullLimits_Pass()
    {
        var c = new FormatConfig { MaxString = null, MaxLength = null, MaxDepth = null };

        c.Validate();

        Assert.Null(c.MaxLength);
    }
}
