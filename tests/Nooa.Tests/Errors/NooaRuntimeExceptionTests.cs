// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class NooaRuntimeExceptionTests
{
    [Fact]
    public void Constructor_Message_KeepsMessage()
    {
        var e = new NooaRuntimeException("boom");

        Assert.Equal("boom", e.Message);
    }

    [Fact]
    public void Type_DerivesFromBase()
    {
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(NooaRuntimeException)));
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(NooaRuntimeException)));
    }

    [Fact]
    public void Constructor_MessageAndInner_KeepsInner()
    {
        var inner = new InvalidOperationException("inner");

        var e = new NooaRuntimeException("boom", inner);

        Assert.Same(inner, e.InnerException);
    }
}
