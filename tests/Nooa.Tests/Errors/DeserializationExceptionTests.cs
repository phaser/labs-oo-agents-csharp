// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class DeserializationExceptionTests
{
    [Fact]
    public void Constructor_Message_KeepsMessage()
    {
        var e = new DeserializationException("boom");

        Assert.Equal("boom", e.Message);
    }

    [Fact]
    public void Type_DerivesFromBase()
    {
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(DeserializationException)));
        Assert.True(typeof(NooaException).IsAssignableFrom(typeof(DeserializationException)));
    }

    [Fact]
    public void Constructor_MessageAndInner_KeepsInner()
    {
        var inner = new InvalidOperationException("inner");

        var e = new DeserializationException("boom", inner);

        Assert.Same(inner, e.InnerException);
    }
}
