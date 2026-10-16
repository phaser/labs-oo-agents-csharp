// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_inherited_fields.py, test_visibility.py, test_classmethod_rendering.py

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class MemberReaderTests
{
    private class Animal
    {
        public string Name = "";
        public int Legs;
        public virtual string Speak() => "";
        public static Animal Create() => new();
    }

    private sealed class Dog : Animal
    {
        public string Breed = "";
        public string Tag { get; init; } = "";
        public int Secret { get; private set; }
        public int Writeonly { private get; set; }
        [Hidden]
        public string Hide = "";
        public string _internal = "";
        private readonly string _private = "";
        public override string Speak() => "woof";
        public override string ToString() => "dog";
        public void Fetch(string item)
        {
            _ = _private;
            Secret = 0;
        }

        [Hidden]
        public void Concealed()
        {
        }

        public string this[int i] => "";
    }

    private sealed record Point(int X, int Y);

    private static string[] Names(IEnumerable<System.Reflection.MemberInfo> members) => [.. members.Select(m => m.Name)];

    [Fact]
    public void GetFields_Inherited_BaseFieldsComeFirst()
    {
        var names = Names(MemberReader.GetFields(typeof(Dog)));
        Assert.Equal(["Name", "Legs", "Breed", "Tag", "Secret", "Writeonly"], names);
    }

    [Fact]
    public void GetFields_HiddenPrivateAndUnderscore_AreExcluded()
    {
        var names = Names(MemberReader.GetFields(typeof(Dog)));
        Assert.DoesNotContain("Hide", names);
        Assert.DoesNotContain("_internal", names);
        Assert.DoesNotContain("_private", names);
        Assert.DoesNotContain("Item", names);
    }

    [Fact]
    public void GetMethods_OverrideAndObjectMembers_AppearOnce()
    {
        var names = Names(MemberReader.GetMethods(typeof(Dog)));
        Assert.Equal(["Speak", "Fetch", "Create"], names);
    }

    [Fact]
    public void GetMethods_Record_SkipsGeneratedMembers()
    {
        Assert.Empty(MemberReader.GetMethods(typeof(Point)));
        Assert.Equal(["X", "Y"], Names(MemberReader.GetFields(typeof(Point))));
    }

    [Fact]
    public void IsHidden_Marked_ReturnsTrue()
    {
        Assert.True(MemberReader.IsHidden(typeof(Dog).GetField("Hide")!));
        Assert.False(MemberReader.IsHidden(typeof(Dog).GetField("Breed")!));
    }
}
