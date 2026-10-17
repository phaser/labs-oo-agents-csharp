// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_decorator_strategy.py

using Nooa.Config;
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

    private static CurrentCall WithArguments(params (string Name, object? Value)[] arguments) => new()
    {
        MethodName = "m",
        Arguments = [.. arguments.Select(a => new CallArgument(a.Name, a.Value?.GetType() ?? typeof(object), a.Value))],
    };

    [Fact]
    public void FormatParametersAsCode_NoArguments_ReturnsEmptyString()
    {
        Assert.Equal("", new CurrentCall { MethodName = "m" }.FormatParametersAsCode());
    }

    [Fact]
    public void FormatParametersAsCode_Default_ShowsOneNamedLinePerArgumentInFull()
    {
        var text = WithArguments(("data", "test"), ("threshold", 0.5), ("flag", true)).FormatParametersAsCode();

        Assert.Equal("data = \"test\"\nthreshold = 0.5\nflag = true", text);
    }

    [Fact]
    public void FormatParametersAsCode_LongString_IsNotTruncatedWithoutTruncationConfig()
    {
        var content = "Knowledge guide: " + new string('a', 5000) + " end.";

        var text = WithArguments(("knowledgeMd", content)).FormatParametersAsCode();

        Assert.Contains(content, text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatParametersAsCode_WithTruncationConfig_AppliesThePrefillLimits()
    {
        var content = new string('x', 5291);

        var text = WithArguments(("knowledgeMd", content)).FormatParametersAsCode(truncation: new TruncationConfig());

        Assert.DoesNotContain(content, text, StringComparison.Ordinal);
        Assert.StartsWith("knowledgeMd = ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatParametersAsCode_ValueFormatter_ReceivesTheValuesAndWinsOverTruncation()
    {
        var received = new List<object?>();
        var list = new List<int> { 1, 2, 3 };

        var text = WithArguments(("a", 42), ("items", list)).FormatParametersAsCode(
            v =>
            {
                received.Add(v);
                return "FMT(" + v + ")";
            },
            new TruncationConfig());

        Assert.Equal([42, list], received);
        Assert.Equal("a = FMT(42)\nitems = FMT(" + list + ")", text);
    }
}
