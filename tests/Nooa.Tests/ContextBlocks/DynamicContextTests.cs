// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_models.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class DynamicContextTests
{
    [Fact]
    public void Provider_IsCalledOnEachRead()
    {
        var calls = 0;
        var dynamic = new DynamicContext(() => ++calls);

        Assert.Equal(1, dynamic.Provider());
        Assert.Equal(2, dynamic.Provider());
    }

    [Fact]
    public void Constructor_NullProvider_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new DynamicContext(null!));

    [Fact]
    public void Equals_SameProvider_IsTrue()
    {
        Func<object?> provider = () => "x";

        Assert.Equal(new DynamicContext(provider), new DynamicContext(provider));
        Assert.NotEqual(new DynamicContext(provider), new DynamicContext(() => "x"));
    }
}
