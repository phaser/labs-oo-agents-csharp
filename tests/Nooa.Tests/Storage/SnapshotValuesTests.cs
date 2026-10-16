// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/storage/test_serialization.py (primitives and collections)

using System.Text.Json.Nodes;
using Nooa.Errors;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class SnapshotValuesTests
{
    [Fact]
    public void ToNode_FromNode_RoundTripsJsonLikeValues()
    {
        var value = new Dictionary<string, object?>
        {
            ["s"] = "text",
            ["i"] = 3L,
            ["d"] = 2.5,
            ["b"] = false,
            ["n"] = null,
            ["list"] = new List<object?> { 1L, "x" },
            ["nested"] = new Dictionary<string, object?> { ["k"] = "v" },
        };

        var back = Assert.IsType<Dictionary<string, object?>>(SnapshotValues.FromNode(SnapshotValues.ToNode(value)));

        Assert.Equal("text", back["s"]);
        Assert.Equal(3L, back["i"]);
        Assert.Equal(2.5, back["d"]);
        Assert.Equal(false, back["b"]);
        Assert.Null(back["n"]);
        Assert.Equal([1L, "x"], Assert.IsType<List<object?>>(back["list"]));
        Assert.Equal("v", Assert.IsType<Dictionary<string, object?>>(back["nested"])["k"]);
    }

    [Fact]
    public void ToNode_EnumBecomesText() =>
        Assert.Equal("Green", SnapshotValues.ToNode(SnapshotAgent.Color.Green)!.GetValue<string>());

    [Fact]
    public void ToNode_ArraysAndSetsBecomeLists()
    {
        Assert.Equal("[1,2]", SnapshotValues.ToNode(new[] { 1, 2 })!.ToJsonString());
        Assert.Equal("[\"a\"]", SnapshotValues.ToNode(new HashSet<string> { "a" })!.ToJsonString());
    }

    [Fact]
    public void ToNode_NonStringKey_Throws() =>
        Assert.Throws<SerializationException>(() => SnapshotValues.ToNode(new Dictionary<int, string> { [1] = "a" }));

    [Fact]
    public void ToNode_UnknownObject_ThrowsWithTypeName()
    {
        var e = Assert.Throws<SerializationException>(() => SnapshotValues.ToNode(new object()));

        Assert.Contains("'Object'", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromNode_Null_ReturnsNull() => Assert.Null(SnapshotValues.FromNode(null));

    [Fact]
    public void FromNode_BigNumber_FallsBackToDouble() =>
        Assert.IsType<double>(SnapshotValues.FromNode(JsonNode.Parse("1e30")));
}
