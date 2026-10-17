// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (_jsonable)

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.Json;

namespace Nooa.Strategies;

/// <summary>
/// Converts the value of a cell to the JSON value of an event (D11: events hold JSON, not live objects). Scalars and plain objects
/// keep their JSON form. A collection becomes bounded text from <see cref="PFormat"/>, because a lazy sequence can be endless
/// and the cell value is not trusted (a lazy sequence is read only up to the bound). A value with no JSON form becomes the same text.
/// </summary>
internal static class CellValues
{
    public static JsonNode? ToJson(object? value, FormatConfig format)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonNode node:
                return node.DeepClone();
            case string:
                return Serialize(value, format);
            case System.Collections.ICollection:
                return JsonValue.Create(PFormat.Format(value, format));
            case System.Collections.IEnumerable lazy:
                // A lazy sequence can be endless. Read one item more than the format shows, then format that prefix.
                var prefix = lazy.Cast<object?>().Take((format.MaxLength ?? 1000) + 1).ToList();
                return JsonValue.Create(PFormat.Format(prefix, format));
            default:
                return Serialize(value, format);
        }
    }

    private static JsonNode? Serialize(object value, FormatConfig format)
    {
        try
        {
            return JsonSerializer.SerializeToNode(value, value.GetType(), NooaJson.Options);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return JsonValue.Create(PFormat.Format(value, format));
        }
    }
}
