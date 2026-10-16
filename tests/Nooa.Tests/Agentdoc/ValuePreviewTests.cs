// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class ValuePreviewTests
{
    private enum Color
    {
        Red,
    }

    [Theory]
    [InlineData(null, "null")]
    [InlineData("hi", "\"hi\"")]
    [InlineData("a\"b\n", "\"a\\\"b\\n\"")]
    [InlineData(true, "true")]
    [InlineData(5, "5")]
    [InlineData(1.5, "1.5")]
    [InlineData('x', "'x'")]
    public void Format_Value_ReturnsCSharpLiteral(object? value, string expected)
    {
        Assert.Equal(expected, ValuePreview.Format(value));
    }

    [Fact]
    public void Format_Enum_ReturnsQualifiedName()
    {
        Assert.Equal("Color.Red", ValuePreview.Format(Color.Red));
    }

    [Fact]
    public void Format_Type_ReturnsTypeof()
    {
        Assert.Equal("typeof(List<int>)", ValuePreview.Format(typeof(List<int>)));
    }
}
