// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class ShellWordsTests
{
    [Theory]
    [InlineData("a b  c", new[] { "a", "b", "c" })]
    [InlineData("grep 'a b' \"c d\"", new[] { "grep", "a b", "c d" })]
    [InlineData("a\\ b", new[] { "a b" })]
    [InlineData("\"a\\\"b\"", new[] { "a\"b" })]
    [InlineData("\"a\\nb\"", new[] { "a\\nb" })]
    [InlineData("'a\\nb'", new[] { "a\\nb" })]
    [InlineData("a'b'\"c\"", new[] { "abc" })]
    [InlineData("'' x", new[] { "", "x" })]
    [InlineData("", new string[0])]
    public void Split_Text_FollowsPosixRules(string text, string[] expected)
    {
        Assert.Equal(expected, ShellWords.Split(text));
    }

    [Theory]
    [InlineData("a 'b")]
    [InlineData("a \"b")]
    [InlineData("a\\")]
    public void Split_Unclosed_Throws(string text)
    {
        Assert.Throws<FormatException>(() => ShellWords.Split(text));
    }

    [Theory]
    [InlineData("", "''")]
    [InlineData("abc/def-1.txt", "abc/def-1.txt")]
    [InlineData("a b", "'a b'")]
    [InlineData("it's", "'it'\"'\"'s'")]
    [InlineData("$(x)", "'$(x)'")]
    public void Quote_Value_QuotesWhenUnsafe(string value, string expected)
    {
        Assert.Equal(expected, ShellWords.Quote(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("with space")]
    [InlineData("it's \"quoted\" $HOME `x` \\ \n tab\t")]
    public void Quote_ThenSplit_RoundTrips(string value)
    {
        Assert.Equal([value], ShellWords.Split(ShellWords.Quote(value)));
    }
}
