// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class StructuredOutputExceptionTests
{
    [Fact]
    public void Constructor_KeepsTypeDetailPathContentAndInnerError()
    {
        var inner = new InvalidOperationException("bad");

        var error = new StructuredOutputException(typeof(int), "a value does not match", "oops", "$.x", inner, isMalformedJson: true);

        Assert.Equal("The output is not valid Int32: a value does not match", error.Message);
        Assert.Equal(typeof(int), error.OutputType);
        Assert.Equal("a value does not match", error.Detail);
        Assert.Equal("$.x", error.Path);
        Assert.Equal("oops", error.Content);
        Assert.True(error.IsMalformedJson);
        Assert.Same(inner, error.InnerException);
        Assert.IsAssignableFrom<ValidationException>(error);
    }

    [Fact]
    public void Message_DoesNotContainTheContent()
    {
        var error = new StructuredOutputException(typeof(int), "invalid", "secret-model-text");

        Assert.DoesNotContain("secret-model-text", error.Message);
    }
}
