// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Generators.Models;

namespace Nooa.Generators.Tests;

public class EquatableArrayTests
{
    [Fact]
    public void Equals_SameItems_IsTrueWithSameHash()
    {
        var a = new EquatableArray<string>(["x", "y"]);
        var b = new EquatableArray<string>(new List<string> { "x", "y" });

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentItemsOrLength_IsFalse()
    {
        var a = new EquatableArray<string>(["x", "y"]);

        Assert.NotEqual(a, new EquatableArray<string>(["x", "z"]));
        Assert.NotEqual(a, new EquatableArray<string>(["x"]));
    }

    [Fact]
    public void Default_BehavesAsEmpty()
    {
        EquatableArray<string> none = default;

        Assert.Equal(0, none.Count);
        Assert.Equal(EquatableArray<string>.Empty, none);
        Assert.Empty(none);
    }
}
