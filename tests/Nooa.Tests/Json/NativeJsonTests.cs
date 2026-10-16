// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/_immutable_json.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Json;

namespace Nooa.Tests.Json;

public class NativeJsonTests
{
    [Fact]
    public void Constructor_SourceEditedLater_StoredValueUnchanged()
    {
        var source = new JsonObject { ["a"] = new JsonArray(1, 2) };
        var native = new NativeJson(source);

        source["a"]!.AsArray().Add(3);
        source["b"] = 1;

        Assert.Equal("""{"a":[1,2]}""", native.ToJsonString());
    }

    [Fact]
    public void ToJsonObject_ResultEdited_StoredValueUnchanged()
    {
        var native = new NativeJson(new JsonObject { ["a"] = new JsonArray(1) });

        native.ToJsonObject()["a"]!.AsArray().Add(2);

        Assert.Equal("""{"a":[1]}""", native.ToJsonString());
    }

    [Fact]
    public void Equals_SameStructure_IsTrue()
    {
        var one = new NativeJson(new JsonObject { ["k"] = "v" });
        var two = new NativeJson(new JsonObject { ["k"] = "v" });

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.NotEqual(one, new NativeJson(new JsonObject { ["k"] = "w" }));
    }

    [Fact]
    public void Serialize_RoundTrips()
    {
        var native = new NativeJson(new JsonObject { ["opaque"] = "provider-state", ["n"] = 1 });

        var json = JsonSerializer.Serialize(native);
        var restored = JsonSerializer.Deserialize<NativeJson>(json);

        Assert.Equal(native, restored);
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("\"text\"")]
    [InlineData("1")]
    public void Deserialize_NotAnObject_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeJson>(json));
    }
}
