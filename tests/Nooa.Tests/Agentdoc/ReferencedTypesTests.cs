// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_referenced_types.py

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class ReferencedTypesTests
{
    private sealed class Address;

    private sealed class Customer
    {
        public Address? Home;
        public List<Address> Others = [];
        public string Name = "";
    }

    private sealed class Invoice
    {
        public Customer? Customer;

        public Task<Address[]> Lookup(int id) => Task.FromResult(Array.Empty<Address>());
    }

    private sealed class Loop
    {
        public Loop? Next;
    }

    [Fact]
    public void Discover_Fields_FindsCustomTypesOnly()
    {
        var found = ReferencedTypes.Discover(typeof(Customer));
        Assert.Equal([typeof(Address)], found);
    }

    [Fact]
    public void Discover_MethodSignature_UnwrapsTaskAndArray()
    {
        var found = ReferencedTypes.Discover(typeof(Invoice).GetMethod("Lookup")!);
        Assert.Equal([typeof(Address)], found);
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(List<int>), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(Address), true)]
    public void IsCustom_Type_ReturnsExpected(Type type, bool expected)
    {
        Assert.Equal(expected, ReferencedTypes.IsCustom(type));
    }

    [Fact]
    public void Collect_DepthOne_ReturnsDirectReferencesOnly()
    {
        var seen = new HashSet<Type> { typeof(Invoice) };
        var result = ReferencedTypes.Collect([ReferencedTypes.Discover(typeof(Invoice))], seen, 1);
        Assert.Equal([typeof(Address), typeof(Customer)], result);
    }

    [Fact]
    public void Collect_DepthOne_ExcludesNestedReferences()
    {
        var seen = new HashSet<Type> { typeof(Invoice) };
        var result = ReferencedTypes.Collect([new HashSet<Type> { typeof(Customer) }], seen, 1);
        Assert.Equal([typeof(Customer)], result);
    }

    [Fact]
    public void Collect_DepthTwo_ReturnsTransitiveSortedByName()
    {
        var seen = new HashSet<Type> { typeof(Invoice) };
        var result = ReferencedTypes.Collect([new HashSet<Type> { typeof(Customer) }], seen, 2);
        Assert.Equal([typeof(Address), typeof(Customer)], result);
    }

    [Fact]
    public void Collect_SelfReference_IsNotRepeated()
    {
        var seen = new HashSet<Type> { typeof(Loop) };
        Assert.Empty(ReferencedTypes.Collect([ReferencedTypes.Discover(typeof(Loop))], seen, 3));
    }
}
