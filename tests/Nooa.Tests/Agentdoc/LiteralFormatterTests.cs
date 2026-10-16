// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_pformat_head_tail.py

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class LiteralFormatterTests
{
    private enum Color
    {
        Red,
    }

    private static string Scalar(object value)
    {
        Assert.True(LiteralFormatter.TryFormatScalar(value, out var text));
        return text;
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData(5, "5")]
    [InlineData(-5L, "-5")]
    [InlineData(1.5, "1.5")]
    [InlineData(1.0, "1.0")]
    [InlineData(1e20, "1E+20")]
    [InlineData(2f, "2.0")]
    [InlineData(double.NaN, "double.NaN")]
    [InlineData(double.PositiveInfinity, "double.PositiveInfinity")]
    [InlineData(double.NegativeInfinity, "double.NegativeInfinity")]
    [InlineData('x', "'x'")]
    [InlineData('\'', "'\\''")]
    [InlineData('\n', "'\\n'")]
    public void TryFormatScalar_Value_ReturnsCSharpLiteral(object value, string expected)
    {
        Assert.Equal(expected, Scalar(value));
    }

    [Fact]
    public void TryFormatScalar_Decimal_UsesInvariantCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            Assert.Equal("1.5", Scalar(1.5m));
            Assert.Equal("1.5", Scalar(1.5));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TryFormatScalar_DateTime_UsesIso8601()
    {
        Assert.Equal("2026-10-16T01:02:03.0000000Z", Scalar(new DateTime(2026, 10, 16, 1, 2, 3, DateTimeKind.Utc)));
    }

    [Fact]
    public void TryFormatScalar_EnumAndType_ReturnQualifiedText()
    {
        Assert.Equal("Color.Red", Scalar(Color.Red));
        Assert.Equal("typeof(List<int>)", Scalar(typeof(List<int>)));
    }

    [Fact]
    public void TryFormatScalar_Object_ReturnsFalse()
    {
        Assert.False(LiteralFormatter.TryFormatScalar(new object(), out _));
    }

    [Theory]
    [InlineData("hi", "\"hi\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("back\\slash", "\"back\\\\slash\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("café 世界", "\"café 世界\"")]
    [InlineData("a\nb\u0001", "\"a\\nb\\u0001\"")]
    [InlineData("nul\0", "\"nul\\0\"")]
    [InlineData("", "\"\"")]
    public void String_Value_ReturnsRegularLiteral(string value, string expected)
    {
        Assert.Equal(expected, LiteralFormatter.String(value, null));
    }

    [Fact]
    public void String_LoneSurrogate_IsEscaped()
    {
        Assert.Equal("\"lone\\ud800\"", LiteralFormatter.String("lone" + (char)0xD800, null));
    }

    [Fact]
    public void String_WithLineBreak_ReturnsVerbatimLiteral()
    {
        Assert.Equal("@\"line1\nsay \"\"hi\"\"\\path\"", LiteralFormatter.String("line1\nsay \"hi\"\\path", null));
    }

    [Fact]
    public void String_WithLineBreakAndControlCharacter_ReturnsEscapedLiteral()
    {
        Assert.Equal("\"a\\nb\\u0007\"", LiteralFormatter.String("a\nb\u0007", null));
    }

    [Fact]
    public void String_Emoji_IsKept()
    {
        Assert.Equal("\"🚀\"", LiteralFormatter.String("🚀", null));
    }

    [Fact]
    public void String_AtMaxString_IsComplete()
    {
        Assert.Equal("\"abcde\"", LiteralFormatter.String("abcde", 5));
    }

    [Fact]
    public void String_OverMaxString_ReturnsHeadAndTailMarker()
    {
        Assert.Equal("string(len=11, [:3]=\"abc\", [-2:]=\"jk\")", LiteralFormatter.String("abcdefghijk", 5));
    }

    [Fact]
    public void String_MaxStringOne_ReturnsHeadOnly()
    {
        Assert.Equal("string(len=20, [:1]=\"x\")", LiteralFormatter.String(new string('x', 20), 1));
    }

    [Fact]
    public void Marker_NamesTheTypeAndLengthLabel()
    {
        Assert.Equal("Foo(text_len=4, [:1]=\"a\", [-1:]=\"d\")", LiteralFormatter.Marker("Foo", "text_len", "abcd", 2));
    }
}
