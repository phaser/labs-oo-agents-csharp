// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_core.py, test_multi_type_doc.py, test_referenced_types.py, test_inherited_fields.py, test_annotated_params.py, test_field_type_docstring.py, test_classmethod_rendering.py, test_visibility_public.py, test_doc_instance_agents.py

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using System.ComponentModel;
using System.Reflection;
using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class AgentDocsTests
{
    private sealed class OrderResult;

    private sealed class Address
    {
        public string Street = "";
    }

    /// <summary>A customer with an address.</summary>
    private sealed class Customer
    {
        public string Name = "";
        public Address? Home;
    }

    private sealed class Order
    {
        public Customer Buyer = new();
        public Customer? Other;
    }

    private sealed class Invoice
    {
        public Order Order = new();
        public Customer Buyer = new();
    }

    private sealed class SimpleA
    {
        public string ValueA = "";
    }

    private sealed class SimpleB
    {
        public int ValueB;
    }

    private sealed class Shelf
    {
        public List<int> Items = [1, 2, 3, 4, 5];
        public string Label = new('x', 80);
    }

    private sealed class Warehouse
    {
        public string Name { get; set; } = "Main";
        public int Count = 3;
        [Hidden]
        public string ApiKey = "secret";
        public string Computed => throw new InvalidOperationException("must not be evaluated");

        public Task<OrderResult> CanFulfillOrder(List<string> items, double budget) => throw new NotSupportedException();

        public static Warehouse Create(int size = 5, string? label = null) => new();

        [Hidden]
        public void Internal()
        {
        }
    }

    private class Animal
    {
        public string Name = "";
    }

    private sealed class Dog : Animal
    {
        public string Breed = "";
    }

    private sealed class Documented
    {
        [Description("Display name")]
        public string Name = "Ada";

        public Customer? Buyer;

        public void Search([Description("The search string")] string query, int limit = 10)
        {
        }

        [return: Description("The hit count.")]
        public int Count() => 0;
    }

    private enum Level
    {
        Low = 1,
        High = 5,
    }

    private static MethodInfo Method<T>(string name) => typeof(T).GetMethod(name)!;

    [Fact]
    public void Doc_Type_RendersFieldsAndMethodSignatures()
    {
        var result = AgentDocs.Doc(typeof(Warehouse), inlineDepth: 0);
        Assert.Equal(
            """
            class Warehouse
            {
                int Count;
                string Name { get; set; }
                string Computed { get; }

                Task<OrderResult> CanFulfillOrder(List<string> items, double budget);
                static Warehouse Create(int size = 5, string? label = null);
            }
            """,
            result);
    }

    [Fact]
    public void Doc_Type_ExcludesHiddenMembers()
    {
        var result = AgentDocs.Doc(typeof(Warehouse));
        Assert.DoesNotContain("ApiKey", result);
        Assert.DoesNotContain("Internal", result);
    }

    [Fact]
    public void Doc_Instance_ShowsValuesAndSkipsComputedProperties()
    {
        var result = AgentDocs.Doc(new Warehouse { Name = "East", Count = 7 }, inlineDepth: 0);
        Assert.Contains("string Name { get; set; } = \"East\";", result);
        Assert.Contains("int Count = 7;", result);
        Assert.Contains("string Computed { get; }", result);
        Assert.DoesNotContain("Computed { get; } =", result);
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public void Doc_Instance_CutsLongValuesWithMarkers()
    {
        var result = AgentDocs.Doc(new Shelf(), inlineDepth: 0);
        Assert.Contains("List<int> Items = List<int>(len=5, [:2]=[1, 2], [-1:]=[5]);", result);
        Assert.Contains("string Label = string(len=80, [:25]=", result);
    }

    [Fact]
    public void Doc_InheritedFields_BaseFirst()
    {
        var result = AgentDocs.Doc(typeof(Dog));
        Assert.Contains("class Dog : Animal", result);
        Assert.True(result.IndexOf("string Name;", StringComparison.Ordinal) < result.IndexOf("string Breed;", StringComparison.Ordinal));
    }

    [Fact]
    public void Doc_Enum_ShowsMembersWithValues()
    {
        var result = AgentDocs.Doc(typeof(Level));
        Assert.Equal("enum Level\n{\n    Low = 1,\n    High = 5,\n}", result);
    }

    [Fact]
    public void Doc_Method_UsesQualifiedName()
    {
        var result = AgentDocs.Doc(Method<Warehouse>("CanFulfillOrder"), inlineDepth: 0);
        Assert.Equal("Task<OrderResult> Warehouse.CanFulfillOrder(List<string> items, double budget);", result);
    }

    [Fact]
    public void Doc_Method_IncludesReferencedTypes()
    {
        var result = AgentDocs.Doc(Method<Warehouse>("CanFulfillOrder"));
        Assert.Contains("## Referenced Types", result);
        Assert.Contains("class OrderResult", result);
    }

    [Fact]
    public void Doc_ReferencedTypes_AppearOnceAndSorted()
    {
        var result = AgentDocs.Doc(typeof(Order));
        Assert.Contains("## Referenced Types", result);
        Assert.Equal(1, Count(result, "class Customer"));
        Assert.Equal(0, Count(result, "class Address"));
    }

    [Fact]
    public void Doc_InlineDepthTwo_IncludesTransitiveTypes()
    {
        var result = AgentDocs.Doc(typeof(Order), inlineDepth: 2);
        Assert.Equal(1, Count(result, "class Address"));
        Assert.True(result.IndexOf("class Address", StringComparison.Ordinal) < result.IndexOf("class Customer", StringComparison.Ordinal));
    }

    [Fact]
    public void Doc_InlineDepthZero_OmitsReferencedTypes()
    {
        Assert.DoesNotContain("## Referenced Types", AgentDocs.Doc(typeof(Order), inlineDepth: 0));
    }

    [Fact]
    public void Doc_NoReferences_OmitsSection()
    {
        Assert.DoesNotContain("## Referenced Types", AgentDocs.Doc(typeof(SimpleA)));
    }

    [Fact]
    public void Doc_MultipleTypes_DocumentsEach()
    {
        var result = AgentDocs.Doc([typeof(SimpleA), typeof(SimpleB)]);
        Assert.Contains("class SimpleA", result);
        Assert.Contains("class SimpleB", result);
        Assert.Contains("string ValueA;", result);
        Assert.Contains("int ValueB;", result);
    }

    [Fact]
    public void Doc_MultipleTypes_SharedReferenceAppearsOnce()
    {
        var result = AgentDocs.Doc([typeof(Order), typeof(Invoice)], inlineDepth: 1);
        Assert.Equal(1, Count(result, "class Customer"));
        Assert.Equal(1, Count(result, "## Referenced Types"));
        Assert.Equal(1, Count(result, "class Order"));
    }

    [Fact]
    public void Doc_MultipleInstances_PrimaryTypesAreNotReferenced()
    {
        var result = AgentDocs.Doc([new Order(), new Customer()], inlineDepth: 1);
        Assert.Equal(1, Count(result, "class Customer"));
    }

    [Fact]
    public void Doc_Concise_KeepsFirstLineOnly()
    {
        var fake = new FakeDocProvider();
        fake.Summaries[typeof(Warehouse)] = "First line.\nSecond line.";
        Assert.Contains("/// Second line.", AgentDocs.Doc(typeof(Warehouse), provider: fake));
        var concise = AgentDocs.Doc(typeof(Warehouse), concise: true, provider: fake);
        Assert.Contains("/// First line.", concise);
        Assert.DoesNotContain("Second line", concise);
    }

    [Fact]
    public void Doc_Provider_SuppliesTypeMemberAndParameterText()
    {
        var fake = new FakeDocProvider();
        var search = Method<Documented>("Search");
        fake.Summaries[typeof(Documented)] = "A documented agent.";
        fake.Summaries[search] = "Search the index.";
        fake.Parameters[search.GetParameters()[1]] = "Max results to return";
        var result = AgentDocs.Doc(typeof(Documented), inlineDepth: 0, provider: fake);
        Assert.StartsWith("/// A documented agent.\nclass Documented", result);
        Assert.Contains("    /// Search the index.\n    /// Args:\n    ///   limit: Max results to return\n    void Search(string query, int limit = 10);", result);
    }

    [Fact]
    public void Doc_DescriptionAttribute_ShowsInlineAndInArgs()
    {
        var result = AgentDocs.Doc(typeof(Documented), inlineDepth: 0);
        Assert.Contains("string Name;  // Display name", result);
        Assert.Contains("///   query: The search string", result);
        Assert.Contains("/// Returns: The hit count.", result);
    }

    [Fact]
    public void Doc_FieldWithoutDescription_UsesFirstLineOfTypeSummary()
    {
        var fake = new FakeDocProvider();
        fake.Summaries[typeof(Customer)] = "A customer.\nMore.";
        var result = AgentDocs.Doc(typeof(Order), inlineDepth: 0, provider: fake);
        Assert.Contains("Customer Buyer;  // A customer.", result);
        Assert.Contains("Customer? Other;  // A customer.", result);
    }

    [Fact]
    public void Doc_NonCustomInstance_UsesPFormat()
    {
        Assert.Equal("\"hi\"", AgentDocs.Doc("hi"));
    }

    [Fact]
    public void Doc_NoObjects_Throws()
    {
        Assert.Throws<ArgumentException>(() => AgentDocs.Doc(Array.Empty<object>()));
    }

    [Fact]
    public void Doc_NegativeInlineDepth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentDocs.Doc(typeof(SimpleA), inlineDepth: -1));
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
