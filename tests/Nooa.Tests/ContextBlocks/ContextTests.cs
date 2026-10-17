// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_cached_renderer.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class ContextTests
{
    [Fact]
    public void Of_Literal_IsNotDynamicAndDefaultsToTheVolatileSuffix()
    {
        var context = Context.Of("text");

        Assert.Equal("text", context.Value);
        Assert.False(context.IsDynamic);
        Assert.False(context.Prefix);
        Assert.Null(context.ToDynamicContext());
    }

    [Fact]
    public void Of_WithPrefix_MarksTheCacheablePrefix() => Assert.True(Context.Of("text", prefix: true).Prefix);

    [Fact]
    public void Of_Null_Throws() => Assert.Throws<ArgumentNullException>(() => Context.Of(null!));

    [Fact]
    public void FromProvider_IsDynamicAndConvertsToDynamicContext()
    {
        Func<object?> provider = () => 7;

        var context = Context.FromProvider(provider, prefix: true);

        Assert.True(context.IsDynamic);
        Assert.True(context.Prefix);
        Assert.Null(context.Value);
        Assert.Same(provider, context.ToDynamicContext()!.Provider);
    }

    [Fact]
    public void FromProvider_Null_Throws() => Assert.Throws<ArgumentNullException>(() => Context.FromProvider(null!));

    [Fact]
    public void Expr_IsDynamicWithTheTextAndNoProvider()
    {
        var context = Context.Expr("self.Count", prefix: true);

        Assert.True(context.IsDynamic);
        Assert.True(context.Prefix);
        Assert.Equal("self.Count", context.Expression);
        Assert.Null(context.Value);
        Assert.Null(context.Provider);
        Assert.Null(context.ToDynamicContext());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Expr_Blank_Throws(string text) => Assert.Throws<ArgumentException>(() => Context.Expr(text));

    [Fact]
    public void Expr_Null_Throws() => Assert.Throws<ArgumentNullException>(() => Context.Expr(null!));

    [Fact]
    public void Expr_TooLong_Throws() =>
        Assert.Throws<ArgumentException>(() => Context.Expr(new string('1', Context.MaxExpressionLength + 1)));
}
