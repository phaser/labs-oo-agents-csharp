// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_events.py (test_event_max_string_override)

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using Nooa.Agentdoc;

namespace Nooa.Tests;

public class MaxStringAttributeTests
{
    private sealed class Sample
    {
        public string Plain = "";

        [MaxString]
        public string Unlimited = "";

        [MaxString(300)]
        public string Wide = "";

        [MaxString]
        public List<string> Items = [];
    }

    private static readonly string _long = new('x', 250);

    [Fact]
    public void Constructor_NoArgument_MeansNoLimit() => Assert.Null(new MaxStringAttribute().Limit);

    [Fact]
    public void Constructor_Limit_IsKept() => Assert.Equal(20_000, new MaxStringAttribute(20_000).Limit);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NotPositive_Throws(int limit) => Assert.Throws<ArgumentOutOfRangeException>(() => new MaxStringAttribute(limit));

    [Fact]
    public void Format_Member_UsesItsOwnLimit()
    {
        var sample = new Sample { Plain = _long, Unlimited = _long, Wide = _long };

        var text = PFormat.Format(sample, maxString: 50);

        Assert.Contains("Plain = string(len=250", text);
        Assert.Contains("Unlimited = \"" + _long + "\"", text);
        Assert.Contains("Wide = \"" + _long + "\"", text);
    }

    [Fact]
    public void Format_WideLimit_StillCutsALongerString()
    {
        var sample = new Sample { Wide = new string('y', 400) };

        Assert.Contains("Wide = string(len=400", PFormat.Format(sample, maxString: 50));
    }

    [Fact]
    public void Format_Override_CoversNestedValuesOfTheMember_ButNotTheNextMember()
    {
        var sample = new Sample { Items = [_long], Plain = _long };

        var text = PFormat.Format(sample, maxString: 50);

        Assert.Contains("\"" + _long + "\"", text);
        Assert.Contains("Plain = string(len=250", text);
    }
}
