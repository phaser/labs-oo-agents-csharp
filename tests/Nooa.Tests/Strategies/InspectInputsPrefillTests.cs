// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_prefill.py (intent)

using Nooa.Config;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

public class InspectInputsPrefillTests
{
    private static CurrentCall Call(Type? returnType, params CallArgument[] arguments) =>
        new() { MethodName = "Analyze", ReturnType = returnType, Arguments = arguments };

    [Fact]
    public void GetCode_NoArguments_ReturnsNull()
    {
        Assert.Null(new InspectInputsPrefill().GetCode(Call(typeof(int))));
    }

    [Fact]
    public void GetCode_Arguments_PrintsEachWithTheDeclaredTypeAndTheConfiguredLimits()
    {
        var config = TruncationConfig.Default with { PrefillFormat = new FormatConfig { MaxLength = 7, MaxString = 80, MaxDepth = null } };

        var code = new InspectInputsPrefill().GetCode(
            Call(typeof(int), new CallArgument("items", typeof(List<string>), null), new CallArgument("budget", typeof(double), 1.0)), config);

        Assert.Equal(
            "// Inspecting inputs for Analyze().\n"
            + "Print(\"Task: Analyze()\");\n"
            + "Print(\"\\nitems (List<string>):\");\n"
            + "PPrint(items, maxLength: 7, maxString: 80, maxDepth: null);\n"
            + "Print(\"\\nbudget (double):\");\n"
            + "PPrint(budget, maxLength: 7, maxString: 80, maxDepth: null);",
            code);
    }

    [Fact]
    public void GetCode_ComplexReturnType_AddsTheTypeDocumentation()
    {
        var code = new InspectInputsPrefill().GetCode(Call(typeof(OrderResult), new CallArgument("n", typeof(int), 1)));

        Assert.EndsWith(
            "Print(\"\\nReturn type:\");\nDoc(typeof(global::Nooa.Tests.Strategies.Acceptance.OrderResult), concise: true);",
            code,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(List<OrderResult>))]
    [InlineData(typeof(Dictionary<string, int>))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(DateTime))]
    public void GetCode_SimpleReturnType_DoesNotDocumentIt(Type returnType)
    {
        var code = new InspectInputsPrefill().GetCode(Call(returnType, new CallArgument("n", typeof(int), 1)));

        Assert.DoesNotContain("Return type", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCode_NoReturnType_AddsNoDocumentation()
    {
        var code = new InspectInputsPrefill().GetCode(Call(null, new CallArgument("n", typeof(int), 1)));

        Assert.DoesNotContain("Return type", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCode_ArgumentNamedLikeAKeyword_UsesTheVerbatimIdentifier()
    {
        var code = new InspectInputsPrefill().GetCode(Call(typeof(int), new CallArgument("class", typeof(string), "x")));

        Assert.Contains("PPrint(@class,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void IsComplex_Enum_IsComplex()
    {
        Assert.True(InspectInputsPrefill.IsComplex(typeof(DayOfWeek)));
    }
}
