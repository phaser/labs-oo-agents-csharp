// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using System.Reflection;
using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class TypeNameFormatterTests
{
    private sealed class Order;

    private sealed class Holder
    {
        public string? MaybeName = null;
        public List<string?> Names = [];
        public Task<Order> Pending => Task.FromResult(new Order());
        public (int, string) Pair;
        public int? Count;
        public int[,] Grid = new int[0, 0];
        public Dictionary<string, List<Order>> Index = [];
    }

    private static string Of(string field)
    {
        var f = typeof(Holder).GetField(field);
        if (f is not null)
        {
            return TypeNameFormatter.Format(f.FieldType, new NullabilityInfoContext().Create(f));
        }

        var p = typeof(Holder).GetProperty(field)!;
        return TypeNameFormatter.Format(p.PropertyType, new NullabilityInfoContext().Create(p));
    }

    [Theory]
    [InlineData("MaybeName", "string?")]
    [InlineData("Names", "List<string?>")]
    [InlineData("Pending", "Task<Order>")]
    [InlineData("Pair", "(int, string)")]
    [InlineData("Count", "int?")]
    [InlineData("Grid", "int[,]")]
    [InlineData("Index", "Dictionary<string, List<Order>>")]
    public void Format_Member_ReturnsCSharpText(string member, string expected)
    {
        Assert.Equal(expected, Of(member));
    }

    [Fact]
    public void Format_Void_ReturnsKeyword()
    {
        Assert.Equal("void", TypeNameFormatter.Format(typeof(void)));
    }

    [Fact]
    public void Format_GenericDefinition_ShowsParameters()
    {
        Assert.Equal("List<T>", TypeNameFormatter.Format(typeof(List<>)));
    }
}
