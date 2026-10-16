// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class ConfigValidationExceptionTests
{
    [Fact]
    public void Constructor_Message_KeepsMessage()
    {
        var e = new ConfigValidationException("boom");

        Assert.Equal("boom", e.Message);
    }

    [Fact]
    public void Type_DerivesFromBase()
    {
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(ConfigValidationException)));
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(ConfigValidationException)));
    }
}
