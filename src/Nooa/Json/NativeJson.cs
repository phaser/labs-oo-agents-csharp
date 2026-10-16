// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/_immutable_json.py

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Nooa.Json;

/// <summary>
/// An immutable, opaque JSON object for adapter-only extensions, such as provider state that must be replayed.
/// </summary>
/// <remarks>
/// The constructor takes a deep copy, so later edits of the source do not change this value.
/// <see cref="ToJsonObject"/> returns a fresh copy, so callers cannot change the stored value.
/// Equality compares the JSON structure.
/// </remarks>
[JsonConverter(typeof(NativeJsonConverter))]
public sealed class NativeJson : IEquatable<NativeJson>
{
    private readonly JsonObject _value;

    /// <summary>Creates a value from a deep copy of <paramref name="value"/>.</summary>
    public NativeJson(JsonObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = (JsonObject)value.DeepClone();
    }

    /// <summary>Number of top-level properties. An empty value counts as no state.</summary>
    public int Count => _value.Count;

    /// <summary>Returns a mutable copy of the stored object.</summary>
    public JsonObject ToJsonObject() => (JsonObject)_value.DeepClone();

    /// <summary>Returns the JSON text.</summary>
    public string ToJsonString() => _value.ToJsonString();

    /// <inheritdoc />
    public bool Equals(NativeJson? other) => other is not null && JsonNode.DeepEquals(_value, other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NativeJson);

    /// <inheritdoc />
    public override int GetHashCode() => _value.Count;

    private sealed class NativeJsonConverter : JsonConverter<NativeJson>
    {
        public override NativeJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);
            return node is JsonObject obj
                ? new NativeJson(obj)
                : throw new JsonException("A native extension must be a JSON object");
        }

        public override void Write(Utf8JsonWriter writer, NativeJson value, JsonSerializerOptions options) =>
            value._value.WriteTo(writer);
    }
}
