// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/snapshot_vars.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Errors;
using Nooa.Json;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class SnapshotVarsTests
{
    [Fact]
    public void Vars_WritesAreKeptAndSerializedAsJsonObject()
    {
        var vars = new SnapshotVars();
        vars.Vars().Set("name", "Ada");
        vars.Vars().Set("n", 3);

        var json = JsonSerializer.Serialize(vars, NooaJson.Options);

        Assert.Equal("{\"name\":\"Ada\",\"n\":3}", json);
    }

    [Fact]
    public void Deserialize_RestoresValues()
    {
        var restored = JsonSerializer.Deserialize<SnapshotVars>("{\"name\":\"Ada\",\"list\":[1,2]}", NooaJson.Options)!;

        Assert.Equal("Ada", restored.Vars().Get<string>("name"));
        Assert.Equal([1, 2], restored.Vars().Get<List<int>>("list"));
        Assert.Equal(2, restored.Count);
    }

    [Fact]
    public void Deserialize_NotAnObject_Throws()
    {
        Assert.Throws<DeserializationException>(() => JsonSerializer.Deserialize<SnapshotVars>("[1]", NooaJson.Options));
    }

    [Fact]
    public void Constructor_CopiesTheState()
    {
        var state = new JsonObject { ["a"] = 1 };
        var vars = new SnapshotVars(state);

        state["a"] = 2;

        Assert.Equal(1, vars.Vars().Get<int>("a"));
    }

    [Fact]
    public void Type_IsEligibleForSnapshots()
    {
        Assert.True(SnapshotTypes.IsEligible(typeof(SnapshotVars)));
    }

    [Fact]
    public void Vars_NonSerializableValue_IsDropped()
    {
        var vars = new SnapshotVars();

        Assert.False(vars.Vars().Set("client", new object()));

        Assert.Equal(0, vars.Count);
    }

    [Fact]
    public void Snapshot_AgentField_RoundTripsThroughStorage()
    {
        using var storage = new SqliteStorageManager();
        var source = new VarsAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() });
        source.Memory.Vars().Set("user", "Ada");
        source.Memory.Vars().Set("tags", new List<string> { "a", "b" });
        storage.SaveSnapshot(source);
        var target = new VarsAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() });

        Assert.True(storage.RestoreLatestSnapshot(target));

        Assert.Equal("Ada", target.Memory.Vars().Get<string>("user"));
        Assert.Equal(["a", "b"], target.Memory.Vars().Get<List<string>>("tags"));
    }
}
