// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class StructuredOutputExceptionTests
{
    [Fact]
    public void Constructor_KeepsTypeContentAndInnerError()
    {
        var inner = new InvalidOperationException("bad");

        var error = new StructuredOutputException("not valid", typeof(int), "oops", inner);

        Assert.Equal("not valid", error.Message);
        Assert.Equal(typeof(int), error.OutputType);
        Assert.Equal("oops", error.Content);
        Assert.Same(inner, error.InnerException);
        Assert.IsAssignableFrom<ValidationException>(error);
    }
}
