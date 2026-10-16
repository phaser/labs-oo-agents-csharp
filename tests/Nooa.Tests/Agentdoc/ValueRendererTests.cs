// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_pprint_respects_hidden.py

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class ValueRendererTests
{
    private sealed record Person(string Name, int Age);

    private sealed class Wrapper
    {
        public Person Inner { get; init; } = new("Ada", 36);
        public List<int> Numbers { get; init; } = [1, 2, 3];
    }

    private sealed class Account
    {
        public string Host = "prod.example.com";
        [Hidden]
        public string Password = "hunter2";
        public string _cache = "internal";
        public Action? Callback = () => { };
        public int Count { get; init; } = 5;
        public string Computed => throw new InvalidOperationException("getter must not run");

        [Hidden]
        public string Secret { get; init; } = "sk-secret";
    }

    private sealed class VirtualOverride : VirtualBase
    {
        public override int Value => throw new InvalidOperationException("computed override must not run");
    }

    private class VirtualBase
    {
        public virtual int Value { get; init; } = 7;
    }

    private sealed class CustomText
    {
        public string Hidden = "not shown";
        public override string ToString() => "custom-text";
    }

    private sealed class ThrowingText
    {
        public override string ToString() => throw new InvalidOperationException();
    }

    private sealed class Node
    {
        public string Name = "";
        public Node? Next;
    }

    private struct Point
    {
        public int X;
        public int Y;
    }

    private sealed class Wide
    {
        public int A = 1;
        public int B = 2;
        public int C = 3;
    }

    [Flags]
    private enum Perm
    {
        R = 1,
        W = 2,
    }

    [Fact]
    public void Write_Record_ShowsMembersInDeclarationOrder()
    {
        Assert.Equal("Person { Name = \"Ada\", Age = 36 }", PFormat.Format(new Person("Ada", 36)));
    }

    [Fact]
    public void Write_Struct_ShowsFields()
    {
        Assert.Equal("Point { X = 3, Y = 4 }", PFormat.Format(new Point { X = 3, Y = 4 }));
    }

    [Fact]
    public void Write_NestedObjects_ShowMembersRecursively()
    {
        Assert.Equal("Wrapper { Inner = Person { Name = \"Ada\", Age = 36 }, Numbers = [1, 2, 3] }", PFormat.Format(new Wrapper()));
    }

    [Fact]
    public void Write_Object_OmitsHiddenUnderscoreDelegateAndComputedMembers()
    {
        var result = PFormat.Format(new Account());
        Assert.Equal("Account { Host = \"prod.example.com\", Count = 5 }", result);
        Assert.DoesNotContain("hunter2", result, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ObjectInContainer_OmitsHiddenMembers()
    {
        var result = PFormat.Format(new Dictionary<string, object> { ["nested"] = new Account() });
        Assert.DoesNotContain("hunter2", result, StringComparison.Ordinal);
        Assert.Contains("Host", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ComputedOverrideOfAutoProperty_IsLeftOutAndNotRun()
    {
        Assert.Equal("VirtualOverride { }", PFormat.Format(new VirtualOverride()));
    }

    [Fact]
    public void Write_TypeWithOwnToString_UsesThatText()
    {
        Assert.Equal("custom-text", PFormat.Format(new CustomText()));
    }

    [Fact]
    public void Write_ToStringThatThrows_ShowsTypeName()
    {
        Assert.Equal("<ThrowingText>", PFormat.Format(new ThrowingText()));
    }

    [Fact]
    public void Write_LongText_UsesTextLengthMarker()
    {
        var result = PFormat.Format(new Uri("https://example.com/" + new string('a', 50)), maxString: 10);
        Assert.StartsWith("Uri(text_len=", result, StringComparison.Ordinal);
        Assert.Contains("[:5]=\"https\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_FrameworkObject_UsesToStringAndNotMembers()
    {
        Assert.Equal("System.Object", PFormat.Format(new object()));
        Assert.Equal("00000000-0000-0000-0000-000000000000", PFormat.Format(Guid.Empty));
    }

    [Fact]
    public void Write_ObjectCycle_ShowsCycle()
    {
        var node = new Node { Name = "a" };
        node.Next = node;
        Assert.Equal("Node { Name = \"a\", Next = <cycle> }", PFormat.Format(node));
    }

    [Fact]
    public void Write_ObjectWithNullMember_ShowsNull()
    {
        Assert.Equal("Node { Name = \"a\", Next = null }", PFormat.Format(new Node { Name = "a" }));
    }

    [Fact]
    public void Write_ObjectOverMaxLength_ShowsEllipsis()
    {
        Assert.Equal("Wide { A = 1, B = 2, ... }", PFormat.Format(new Wide(), maxLength: 2));
        Assert.Equal("Wide { A = 1, B = 2, C = 3 }", PFormat.Format(new Wide(), maxLength: 3));
    }

    [Fact]
    public void Write_ObjectAtMaxDepth_ShowsShallowForm()
    {
        Assert.Equal("Wrapper { Inner = Person { ... }, Numbers = [List<int>: 3 items] }", PFormat.Format(new Wrapper(), maxDepth: 1));
    }

    [Fact]
    public void Write_CollectionsAtMaxDepth_ShowShallowForm()
    {
        var nested = new List<object> { new List<int> { 1, 2 }, new Dictionary<string, int> { ["a"] = 1 }, new List<int>() };
        Assert.Equal("[[List<int>: 2 items], {Dictionary<string, int>: 1 items}, []]", PFormat.Format(nested, maxDepth: 1));
    }

    [Fact]
    public void Write_StringAtMaxDepth_StillHonorsMaxString()
    {
        Assert.Equal("[\"abc\"]", PFormat.Format(new List<string> { "abc" }, maxDepth: 2));
    }

    [Fact]
    public void Write_Enum_ShowsQualifiedNames()
    {
        Assert.Equal("Perm.R", PFormat.Format(Perm.R));
        Assert.Equal("Perm.R | Perm.W", PFormat.Format(Perm.R | Perm.W));
        Assert.Equal("(Perm)8", PFormat.Format((Perm)8));
    }

    [Fact]
    public void Write_LazyEnumerable_IsNotEnumerated()
    {
        var enumerated = false;
        IEnumerable<int> Lazy()
        {
            enumerated = true;
            yield return 1;
        }

        PFormat.Format(Lazy());
        Assert.False(enumerated);
    }

    [Fact]
    public void Write_DictionaryWithNonStringKeys_UsesLiteralKeys()
    {
        Assert.Equal("{1: \"a\", 2: \"b\"}", PFormat.Format(new Dictionary<int, string> { [1] = "a", [2] = "b" }));
    }

    [Fact]
    public void Write_LongStringKey_IsCutAtFiftyCharacters()
    {
        var result = PFormat.Format(new Dictionary<string, int> { [new string('k', 80)] = 1 });
        Assert.StartsWith("{string(len=80, [:25]=", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_Null_ShowsNull()
    {
        Assert.Equal("null", PFormat.Format(null));
        Assert.Equal("[null]", PFormat.Format(new List<string?> { null }));
    }

    [Fact]
    public void Write_Delegate_ShowsTypeInAngleBrackets()
    {
        Func<int, int> f = x => x;
        Assert.Equal("[<Func<int, int>>]", PFormat.Format(new List<object> { f }));
    }
}
