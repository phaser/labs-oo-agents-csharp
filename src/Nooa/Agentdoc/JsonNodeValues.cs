// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nooa.Agentdoc;

/// <summary>Turns a <see cref="JsonNode"/> into plain values (dictionary, list, scalar) so that <c>PFormat</c> prints it like any other value.</summary>
internal static class JsonNodeValues
{
    public static object? ToPlain(JsonNode? node) => node switch
    {
        null => null,
        JsonObject obj => obj.ToDictionary(p => p.Key, p => ToPlain(p.Value)),
        JsonArray array => array.Select(ToPlain).ToList(),
        JsonValue value => Scalar(value),
        _ => node.ToJsonString(),
    };

    private static object? Scalar(JsonValue value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? (object)integer : element.GetDouble(),
            _ => null,
        };
    }
}
