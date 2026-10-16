// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_decorator_strategy.py

using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class CurrentCallTests
{
    [Fact]
    public void New_GeneratesDistinctPrefixedIds()
    {
        var a = new CurrentCall { MethodName = "m" };
        var b = new CurrentCall { MethodName = "m" };

        Assert.StartsWith("call_", a.Id, StringComparison.Ordinal);
        Assert.Equal("call_".Length + 12, a.Id.Length);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void New_Defaults_HaveNoArgumentsOrOptionalData()
    {
        var call = new CurrentCall { MethodName = "m" };

        Assert.Empty(call.Arguments);
        Assert.Null(call.Signature);
        Assert.Null(call.Docstring);
        Assert.Null(call.ReturnType);
        Assert.Null(call.StrategyType);
        Assert.Null(call.ParentId);
    }

    [Fact]
    public void With_KeepsTheStrategyTypeOfTheGeneratedCall()
    {
        var call = new CurrentCall { MethodName = "m", StrategyType = typeof(string) };

        Assert.Equal(typeof(string), (call with { ParentId = "p" }).StrategyType);
    }
}
