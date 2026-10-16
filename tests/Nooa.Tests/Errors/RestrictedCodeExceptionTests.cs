// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class RestrictedCodeExceptionTests
{
    [Fact]
    public void Constructor_MessageOnly_HasNoLineNumberOrInner()
    {
        var e = new RestrictedCodeException("forbidden");

        Assert.Equal("forbidden", e.Message);
        Assert.Null(e.LineNumber);
        Assert.Null(e.InnerException);
    }

    [Fact]
    public void Constructor_LineNumberAndInner_KeepsBoth()
    {
        var inner = new InvalidOperationException("inner");

        var e = new RestrictedCodeException("forbidden", 7, inner);

        Assert.Equal(7, e.LineNumber);
        Assert.Same(inner, e.InnerException);
    }

    [Fact]
    public void Type_DerivesFromValidationException()
    {
        Assert.IsAssignableFrom<ValidationException>(new RestrictedCodeException("x"));
    }
}
