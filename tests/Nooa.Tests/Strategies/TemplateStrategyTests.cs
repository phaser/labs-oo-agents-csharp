// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/template.py

using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class TemplateStrategyTests
{
    private static Task<object?> Run(CurrentCall call) =>
        new TemplateStrategy().ExecuteAsync(null!, call, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExecuteAsync_WithDocstring_ReturnsTheText()
    {
        var result = await Run(new CurrentCall { MethodName = "m", Docstring = "Hello Alice, you have 5 messages." });

        Assert.Equal("Hello Alice, you have 5 messages.", result);
    }

    [Fact]
    public async Task ExecuteAsync_NoDocstring_ReturnsEmptyString()
    {
        Assert.Equal("", await Run(new CurrentCall { MethodName = "m" }));
    }

    [Fact]
    public void Properties_DescribeAStrategyWithoutModelCall()
    {
        var strategy = new TemplateStrategy();

        Assert.Equal("TEMPLATE", strategy.Name);
        Assert.False(strategy.Traceable);
        Assert.False(strategy.RequiresLock);
    }
}
