// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Generators.Models;

namespace Nooa.Generators.Tests;

public class PromptParserTests
{
    private static string? Resolve(string path) => path == "Bad" ? null : "a." + path;

    [Fact]
    public void Parse_PlainText_ReturnsLiteral()
    {
        var prompt = PromptParser.Parse("Hello world.", Resolve, out var error);

        Assert.Null(error);
        Assert.Equal(new PromptModel("Hello world.", EquatableArray<string>.Empty), prompt);
        Assert.False(prompt!.HasPlaceholders);
    }

    [Fact]
    public void Parse_Placeholders_ReturnsFormatAndExpressions()
    {
        var prompt = PromptParser.Parse("Hi {Name}, you have {Cart.Count} items.", Resolve, out var error);

        Assert.Null(error);
        Assert.Equal("Hi {0}, you have {1} items.", prompt!.Value);
        Assert.Equal(["a.Name", "a.Cart.Count"], prompt.Arguments);
    }

    [Fact]
    public void Parse_EscapedBraces_AreLiteralAndEscapedInFormat()
    {
        var literal = PromptParser.Parse("Use {{x}} here.", Resolve, out _);
        var format = PromptParser.Parse("Use {{x}} for {Name}.", Resolve, out _);

        Assert.Equal("Use {x} here.", literal!.Value);
        Assert.Equal("Use {{x}} for {0}.", format!.Value);
    }

    [Theory]
    [InlineData("{Bad}")]
    [InlineData("{ Name }")]
    [InlineData("{Name()}")]
    [InlineData("{1st}")]
    [InlineData("{A..B}")]
    [InlineData("{A.}")]
    [InlineData("{}")]
    [InlineData("{Name")]
    [InlineData("stray } brace")]
    public void Parse_InvalidPlaceholder_ReturnsErrorAndNoPrompt(string text)
    {
        var prompt = PromptParser.Parse(text, Resolve, out var error);

        Assert.Null(prompt);
        Assert.False(string.IsNullOrEmpty(error));
    }
}
