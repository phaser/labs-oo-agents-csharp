// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class NooaExceptionTests
{
    [Fact]
    public void Constructor_Message_KeepsMessage()
    {
        var e = new NooaException("boom");

        Assert.Equal("boom", e.Message);
    }

    [Fact]
    public void Type_DerivesFromBase()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(NooaException)));
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(NooaException)));
    }

    [Fact]
    public void Constructor_MessageAndInner_KeepsInner()
    {
        var inner = new InvalidOperationException("inner");

        var e = new NooaException("boom", inner);

        Assert.Same(inner, e.InnerException);
    }
}
