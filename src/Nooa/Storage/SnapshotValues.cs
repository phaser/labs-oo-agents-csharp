// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/serialization.py

using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Errors;

namespace Nooa.Storage;

/// <summary>
/// Converts the plain value of a context block to JSON and back. Only JSON-like values are allowed: null, text,
/// numbers, booleans, lists and dictionaries with text keys. The result of a conversion back is a string, a long,
/// a double, a bool, a <see cref="List{T}"/> of object or a <see cref="Dictionary{TKey, TValue}"/> of object.
/// No type name comes from the file.
/// </summary>
internal static class SnapshotValues
{
    public static JsonNode? ToNode(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                return JsonSerializer.SerializeToNode(value, value.GetType());
            case Enum e:
                return JsonValue.Create(e.ToString());
            case IDictionary dictionary:
                var obj = new JsonObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    obj[entry.Key as string ?? throw new SerializationException(
                        $"Dictionary key {entry.Key} (type: {entry.Key.GetType().Name}) is not a string. JSON requires string keys.")] =
                        ToNode(entry.Value);
                }

                return obj;
            case IEnumerable items:
                var array = new JsonArray();
                foreach (var item in items)
                {
                    array.Add(ToNode(item));
                }

                return array;
            default:
                var name = value.GetType().Name;
                throw new SerializationException(
                    $"Cannot serialize a context block value of type '{name}'. "
                    + "A block value must be null, text, a number, a boolean, a list or a dictionary with text keys.");
        }
    }

    public static object? FromNode(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when v.TryGetValue<bool>(out var b) => b,
            JsonValue v when v.TryGetValue<long>(out var l) => l,
            JsonValue v when v.TryGetValue<double>(out var d) => d,
            JsonArray a => a.Select(FromNode).ToList(),
            JsonObject o => o.ToDictionary(p => p.Key, p => FromNode(p.Value)),
            _ => throw new DeserializationException("Unsupported JSON value in a context block."),
        };
}
