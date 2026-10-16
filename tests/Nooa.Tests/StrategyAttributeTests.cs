// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

public class StrategyAttributeTests
{
    [Fact]
    public void Constructor_StoresStrategyType()
    {
        Assert.Equal(typeof(string), new StrategyAttribute(typeof(string)).StrategyType);
    }

    [Fact]
    public void Usage_TargetsMethodsOnly_NotRepeatable()
    {
        var usage = AgenticAttributeTests.AttributeUsageOf(typeof(StrategyAttribute));

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
