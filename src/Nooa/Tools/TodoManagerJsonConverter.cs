// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/todo.py

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Nooa.Errors;

namespace Nooa.Tools;

/// <summary>Reads and writes a <see cref="TodoManager"/> as its snapshot state (<see cref="TodoManager.ToJson"/>).</summary>
internal sealed class TodoManagerJsonConverter : JsonConverter<TodoManager>
{
    public override TodoManager Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonNode.Parse(ref reader) is not JsonObject state)
        {
            throw new DeserializationException("Todo snapshot state must be a JSON object.");
        }

        return new TodoManager(state);
    }

    public override void Write(Utf8JsonWriter writer, TodoManager value, JsonSerializerOptions options) =>
        value.ToJson().WriteTo(writer);
}
