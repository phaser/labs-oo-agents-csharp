// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/json_snapshot.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Errors;
using Nooa.Json;

namespace Nooa.Storage;

/// <summary>Agent state to and from the JSON text of a snapshot.</summary>
public static class SnapshotJson
{
    /// <summary>Writes the state of an agent as JSON text.</summary>
    /// <exception cref="SerializationException">A context block value is not JSON-like.</exception>
    public static string ToJson(Agent agent) =>
        JsonSerializer.Serialize(AgentSnapshot.FromAgent(agent), NooaJson.Options);

    /// <summary>
    /// Reads snapshot JSON text and restores it into an agent. The version is checked before anything else.
    /// </summary>
    /// <exception cref="SerializationException">The version is not <see cref="AgentSnapshot.CurrentVersion"/>.</exception>
    /// <exception cref="DeserializationException">The text is not a snapshot, or a value is refused.</exception>
    public static void FromJson(string json, Agent agent)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(agent);
        AgentSnapshot snapshot;
        try
        {
            var root = JsonNode.Parse(json) as JsonObject
                ?? throw new DeserializationException("The snapshot is not a JSON object.");
            if (root["version"] is not JsonValue version || !version.TryGetValue<int>(out var number))
            {
                throw new DeserializationException("The snapshot has no integer version.");
            }

            if (number != AgentSnapshot.CurrentVersion)
            {
                throw new SerializationException($"Snapshot version mismatch: expected {AgentSnapshot.CurrentVersion}, got {number}");
            }

            snapshot = root.Deserialize<AgentSnapshot>(NooaJson.Options)
                ?? throw new DeserializationException("The snapshot is empty.");
        }
        catch (JsonException ex)
        {
            throw new DeserializationException("The snapshot is not valid JSON.", ex);
        }

        snapshot.Restore(agent);
    }
}
