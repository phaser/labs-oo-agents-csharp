// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/snapshot_vars.py

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Nooa.Errors;

namespace Nooa.Storage;

/// <summary>Reads and writes a <see cref="SnapshotVars"/> as the JSON object of its values.</summary>
internal sealed class SnapshotVarsJsonConverter : JsonConverter<SnapshotVars>
{
    public override SnapshotVars Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonNode.Parse(ref reader) is not JsonObject state)
        {
            throw new DeserializationException("The state of SnapshotVars must be a JSON object.");
        }

        return new SnapshotVars(state);
    }

    public override void Write(Utf8JsonWriter writer, SnapshotVars value, JsonSerializerOptions options) =>
        value.ToJson().WriteTo(writer);
}
