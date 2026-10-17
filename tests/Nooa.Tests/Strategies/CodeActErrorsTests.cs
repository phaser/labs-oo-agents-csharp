// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_codeact_errors.py (intent)

using Nooa.Config;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

public class CodeActErrorsTests
{
    private static readonly FormatConfig _format = new() { MaxString = 50, MaxLength = 5, MaxDepth = 3 };

    [Fact]
    public void ExpectedSchema_Record_ListsTheMembersWithTypes()
    {
        Assert.Equal(
            "OrderResult { CanFulfill: bool, TotalCost: double, UnavailableItems: List<string> }",
            CodeActErrors.ExpectedSchema(typeof(OrderResult)));
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(int?), "int")]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(List<string>), "List<string>")]
    [InlineData(typeof(Dictionary<string, int>), "Dictionary<string, int>")]
    [InlineData(typeof(DayOfWeek), "DayOfWeek")]
    public void ExpectedSchema_SimpleType_IsTheTypeName(Type type, string expected)
    {
        Assert.Equal(expected, CodeActErrors.ExpectedSchema(type));
    }

    [Theory]
    [InlineData(typeof(string), "\"your answer\"")]
    [InlineData(typeof(int), "42")]
    [InlineData(typeof(long), "42")]
    [InlineData(typeof(double), "3.14")]
    [InlineData(typeof(bool), "true")]
    [InlineData(typeof(List<string>), "new List<string> { \"item1\", \"item2\" }")]
    [InlineData(typeof(List<int>), "new List<int> { 1, 2, 3 }")]
    [InlineData(typeof(List<double>), "new List<double> { ... }")]
    public void Example_SimpleType_GivesACall(Type type, string expected)
    {
        Assert.Equal(expected, CodeActErrors.Example(type));
    }

    [Fact]
    public void Example_Record_IsNull()
    {
        Assert.Null(CodeActErrors.Example(typeof(OrderResult)));
    }

    [Fact]
    public void WrongType_ShowsExpectedGotAndExample()
    {
        var text = CodeActErrors.WrongType(typeof(int), "abc", _format);

        Assert.Equal(
            "return_result() failed - wrong type.\nExpected: int\nGot: string (value: \"abc\")\nExample: ReturnResult(42)",
            text);
    }

    [Fact]
    public void WrongType_NullAndNoExample_OmitsTheExample()
    {
        var text = CodeActErrors.WrongType(typeof(OrderResult), null, _format);

        Assert.EndsWith("Got: null", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Example", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongType_LongValue_IsBoundedByTheFormat()
    {
        var text = CodeActErrors.WrongType(typeof(int), new string('x', 500), _format);

        Assert.True(text.Length < 400);
    }

    [Fact]
    public void JsonMismatch_Path_NamesThePath()
    {
        var text = CodeActErrors.JsonMismatch(typeof(int), "$.a", "ignored", "v", _format);

        Assert.Contains("a value does not match the type at $.a.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored", text, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonMismatch_MissingMembers_UsesTheSerializerMessage()
    {
        var text = CodeActErrors.JsonMismatch(typeof(FeedbackAnalysis), null, "was missing required properties including: 'summary'.", null, _format);

        Assert.StartsWith("return_result() failed - missing required fields:", text, StringComparison.Ordinal);
        Assert.Contains("'summary'", text, StringComparison.Ordinal);
        Assert.Contains("Expected: FeedbackAnalysis {", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NotJson_TellsTheModelToBuildTheObjectInCode()
    {
        var text = CodeActErrors.NotJson(typeof(OrderResult));

        Assert.Contains("cannot be passed as JSON", text, StringComparison.Ordinal);
        Assert.Contains("ReturnResult(variable)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void InvariantFailed_ShowsTheMessageAndTheNextStep()
    {
        Assert.Equal(
            "Method invariant failed: too small\nRevise the answer so it satisfies this invariant, then call return_result() again.",
            CodeActErrors.InvariantFailed("too small"));
    }
}
