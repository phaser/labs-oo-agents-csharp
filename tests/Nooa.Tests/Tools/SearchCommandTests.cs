// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_tools_modern.py (gate truth table and TestIsPureSearchCommand)

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class SearchCommandTests
{
    [Theory]
    [InlineData("grep -rn 'foo' .", true)]
    [InlineData("grep -rni 'foo' .", true)]
    [InlineData("rg -n 'def foo' src/", true)]
    [InlineData("egrep -rn 'a|b' .", true)]
    [InlineData("grep -rn 'foo' . | head -20", true)]
    [InlineData("rg -n 'foo' . | sed 's/x/y/'", false)]
    [InlineData("grep -rn 'foo' . | awk '{print $1}'", false)]
    [InlineData("grep -rno 'foo' .", false)]
    [InlineData("grep -rnA2 'foo' .", false)]
    [InlineData("grep -c 'foo' .", false)]
    [InlineData("rg -l 'foo' .", false)]
    [InlineData("grep -rnP '(?<=x)foo' .", false)]
    [InlineData("grep --pcre2 -n 'foo' .", false)]
    [InlineData("cat foo.py", false)]
    [InlineData("ls -la", false)]
    [InlineData("grep -rn \"pattern\" src/", true)]
    [InlineData("grep -n \"foo\" bar.py", true)]
    [InlineData("cd /testbed && grep -n \"sympy_integers\" sympy/printing/str.py", true)]
    [InlineData("cd /testbed && grep -rn \"max_iter\" sklearn/decomposition/", true)]
    [InlineData("grep -rn \"pattern\" src/ | head -50", true)]
    [InlineData("grep -rn \"pattern\" src/ | tail -5", true)]
    [InlineData("cd /testbed && grep -rn \"max_iter\" sklearn/ | head -50", true)]
    [InlineData("find /testbed -name \"*.py\" | xargs grep -l \"Foo\" | head -30", false)]
    [InlineData("find . -name '*.py' | xargs grep -n foo", true)]
    [InlineData("find . -name '*.py' | xargs grep foo", false)]
    [InlineData("grep -rn \"pattern\" src/ | sort", false)]
    [InlineData("grep -rn \"pattern\" src/ | awk \"{print $1}\"", false)]
    [InlineData("grep -orn \"pattern\" src/", false)]
    [InlineData("grep -crn \"pattern\" src/", false)]
    [InlineData("grep -A2 \"pattern\" src/", false)]
    [InlineData("echo \"hello\" | grep \"h\"", false)]
    public void IsPure_Command_MatchesTheGate(string command, bool expected)
    {
        Assert.Equal(expected, SearchCommand.IsPure(command));
    }

    [Fact]
    public void IsPure_PathologicalInput_DoesNotHang()
    {
        var hostile = "find " + new string(' ', 50_000) + "x |";

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var pure = SearchCommand.IsPure(hostile);

        Assert.False(pure);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Parse_GrepWithFlags_ReturnsPatternPathsAndFlags()
    {
        var parsed = SearchCommand.Parse("grep -rniF 'a b' src tests");

        Assert.NotNull(parsed);
        Assert.Equal("a b", parsed.Pattern);
        Assert.Equal(["src", "tests"], parsed.Paths);
        Assert.True(parsed.IgnoreCase);
        Assert.True(parsed.FixedStrings);
    }

    [Theory]
    [InlineData("grep --color=always x .")]
    [InlineData("grep -e x .")]
    [InlineData("grep -n")]
    [InlineData("grep -n 'unclosed")]
    [InlineData("cat x")]
    public void Parse_UnknownOrBrokenCommand_ReturnsNull(string command)
    {
        Assert.Null(SearchCommand.Parse(command));
    }

    [Fact]
    public void Parse_DoubleDash_TreatsTheRestAsPositional()
    {
        var parsed = SearchCommand.Parse("grep -n -- -pattern file");

        Assert.NotNull(parsed);
        Assert.Equal("-pattern", parsed.Pattern);
        Assert.Equal(["file"], parsed.Paths);
    }
}
