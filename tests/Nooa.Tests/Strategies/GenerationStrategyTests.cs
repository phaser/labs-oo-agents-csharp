// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Strategies;
using Nooa.Tests.Runtime;

namespace Nooa.Tests.Strategies;

public class GenerationStrategyTests
{
    [Fact]
    public void Defaults_AreTraceableLockedAndNamedAfterType()
    {
        var strategy = new EchoStrategy();

        Assert.True(strategy.Traceable);
        Assert.True(strategy.RequiresLock);
        Assert.Equal("EchoStrategy", strategy.Name);
    }

    [Fact]
    public void RequiresLock_CanBeOverridden() => Assert.False(new GateStrategy(false).RequiresLock);

    [Fact]
    public void ContextHooks_DefaultToNoOverridesAndNoOrder()
    {
        var strategy = new EchoStrategy();

        Assert.Null(strategy.GetBlockOverrides());
        Assert.Null(strategy.GetStaticBlockKeys());
        Assert.Null(strategy.GetBlockOrder());
    }

    [Fact]
    public void Type_IsAbstract() => Assert.True(typeof(GenerationStrategy).IsAbstract);
}
