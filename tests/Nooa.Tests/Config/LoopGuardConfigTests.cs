// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class LoopGuardConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValues()
    {
        var c = new LoopGuardConfig();

        Assert.Equal(7, c.RepeatThreshold);
        Assert.Equal(15, c.Window);
        Assert.Equal(500, c.MaxOutcomeChars);
        c.Validate();
    }

    [Fact]
    public void Validate_BoundaryValues_Pass()
    {
        new LoopGuardConfig { RepeatThreshold = 2, Window = 2, MaxOutcomeChars = 0 }.Validate();
    }

    [Fact]
    public void Validate_OutOfRange_ReportsEveryField()
    {
        var ex = Assert.Throws<ConfigValidationException>(
            () => new LoopGuardConfig { RepeatThreshold = 1, Window = 1, MaxOutcomeChars = -1 }.Validate());

        Assert.Equal(
            "Invalid LoopGuardConfig:\n  - RepeatThreshold must be >= 2, got 1\n  - Window must be >= 2, got 1\n  - MaxOutcomeChars must be >= 0, got -1",
            ex.Message);
    }
}
