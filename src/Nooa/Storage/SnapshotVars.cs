// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/snapshot_vars.py

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Nooa.Storage;

/// <summary>
/// A store of durable agent variables that a snapshot can save and restore. It holds JSON values only (D11).
/// </summary>
/// <remarks>
/// Upstream <c>SnapshotVars</c> is a dictionary that drops, with a warning, every value that a snapshot cannot hold.
/// Here <see cref="PersistentVars.Set"/> does the check at write time, so the store always holds JSON.
/// Use a field of this type on an agent, and use <see cref="Vars"/> to read and write the values.
/// The snapshot state is the JSON object of the values. The file never names a type.
/// </remarks>
[Snapshotable]
[JsonConverter(typeof(SnapshotVarsJsonConverter))]
public sealed class SnapshotVars
{
    private readonly Dictionary<string, JsonNode?> _data = [];

    /// <summary>Creates a store. <paramref name="state"/> is the JSON that <see cref="ToJson"/> wrote.</summary>
    public SnapshotVars(JsonObject? state = null)
    {
        foreach (var (key, value) in state ?? [])
        {
            _data[key] = value?.DeepClone();
        }
    }

    /// <summary>The number of stored names.</summary>
    public int Count => _data.Count;

    /// <summary>Returns the view that reads and writes the values.</summary>
    public PersistentVars Vars() => new(_data);

    /// <summary>Returns the values as a JSON object, for a snapshot.</summary>
    public JsonObject ToJson()
    {
        var obj = new JsonObject();
        foreach (var (key, value) in _data)
        {
            obj[key] = value?.DeepClone();
        }

        return obj;
    }
}
