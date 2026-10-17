// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_tools_modern_behavior.py (ShellResult tests)

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class ShellResultTests
{
    [Fact]
    public void Constructor_KeepsMatchesAndTimeoutFlag()
    {
        var match = new Match("example.py", 1, 1, "value\n", Path.Combine(Path.GetTempPath(), "example.py"));

        var result = new ShellResult("value", "", 0, [match], timedOut: true);

        Assert.Same(match, Assert.Single(result.Matches!));
        Assert.True(result.TimedOut);
        Assert.True(result.Success);
    }

    [Fact]
    public void ToString_SuccessWithoutStderr_ShowsOnlyStdout()
    {
        Assert.Equal("BashOutput(stdout=\"hi\")", new ShellResult("hi").ToString());
    }

    [Fact]
    public void ToString_FailureWithEmptyStdout_ShowsStderrAndCode()
    {
        var result = new ShellResult(string.Empty, "boom \"x\"\n", 2);

        Assert.Equal("BashOutput(stdout=\"\", stderr=\"boom \\\"x\\\"\\n\", return_code=2)", result.ToString());
        Assert.False(result.Success);
    }
}
