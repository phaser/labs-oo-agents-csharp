// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/storage/test_serialization.py (type policy)

using System.Text.Json.Nodes;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class SnapshotTypesTests
{
    public static TheoryData<Type, bool> Cases => new()
    {
        { typeof(int), true },
        { typeof(int?), true },
        { typeof(string), true },
        { typeof(DateTimeOffset), true },
        { typeof(Guid), true },
        { typeof(JsonObject), true },
        { typeof(SnapshotAgent.Color), true },
        { typeof(List<string>), true },
        { typeof(IReadOnlyList<int>), true },
        { typeof(Dictionary<string, List<int>>), true },
        { typeof(IReadOnlyDictionary<string, double>), true },
        { typeof(HashSet<string>), true },
        { typeof(int[]), true },
        { typeof(SnapshotAgent.Profile), true },
        { typeof(SnapshotAgent.Shape), true },
        { typeof(SnapshotAgent.PlainBag), true },
        { typeof(Dictionary<int, string>), false },
        { typeof(object), false },
        { typeof(Type), false },
        { typeof(Stream), false },
        { typeof(System.Diagnostics.Process), false },
        { typeof(Action), false },
        { typeof(IntPtr), false },
        { typeof(SnapshotAgent.Legacy), false },
        { typeof(SnapshotAgent.NoSnapshotHolder), false },
        { typeof(List<object>), false },
        { typeof(int[,]), false },
        { typeof(Nooa.Llm.ILlmClient), false },
        { typeof(IEnumerable<Stream>), false },
        { typeof(Agent), false },
        { typeof((int, int)), false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void IsEligible_ReturnsExpected(Type type, bool expected) =>
        Assert.Equal(expected, SnapshotTypes.IsEligible(type));

    [Fact]
    public void IsEligible_SelfReferencingRecord_Terminates() =>
        Assert.True(SnapshotTypes.IsEligible(typeof(Node)));

    [Fact]
    public void MembersOf_StopsAtAgent_AndUsesPropertyNames()
    {
        var names = SnapshotTypes.MembersOf(typeof(SnapshotAgent)).Keys;

        Assert.Contains("Level", names);
        Assert.Contains("_secret", names);
        Assert.DoesNotContain("Scratch", names);
        Assert.DoesNotContain("Skipped", names);
        Assert.DoesNotContain(names, n => n.Contains("k__BackingField", StringComparison.Ordinal));
        Assert.DoesNotContain("AgentId", names);
    }

    [Fact]
    public void NameOf_IncludesAssembly() =>
        Assert.Equal("System.Int32, System.Private.CoreLib", SnapshotTypes.NameOf(typeof(int)));

    [Fact]
    public void IsNoSnapshotValue_DetectsMarkedClassesAndAgents()
    {
        Assert.True(SnapshotTypes.IsNoSnapshotValue(new SnapshotAgent.NoSnapshotHolder()));
        Assert.False(SnapshotTypes.IsNoSnapshotValue(null));
        Assert.False(SnapshotTypes.IsNoSnapshotValue(3));
        Assert.True(Attribute.IsDefined(typeof(Agent), typeof(NoSnapshotAttribute)));
    }

    private sealed record Node(string Name, Node? Next);
}
