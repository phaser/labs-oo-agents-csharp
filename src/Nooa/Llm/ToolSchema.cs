// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Nooa.Json;

namespace Nooa.Llm;

/// <summary>
/// JSON schema helpers for tool parameters and structured output: build a schema from a .NET method or type,
/// inline references, and choose between OpenAI strict mode and a loose fallback.
/// </summary>
/// <remarks>
/// Upstream also sanitizes schemas for Bedrock (`_sanitize_schema_for_bedrock`). That is not ported: the
/// adapter targets OpenAI-compatible endpoints (backlog B09 covers other providers).
/// </remarks>
public static class ToolSchema
{
    private static readonly string[] _combinators = ["anyOf", "oneOf", "allOf"];
    private static readonly string[] _typeKeys = ["type", "anyOf", "oneOf", "allOf", "enum", "const"];

    private static readonly HashSet<string> _strictAllowed =
    [
        "type", "description", "enum", "const", "properties", "required", "items", "additionalProperties",
        "anyOf", "oneOf", "allOf",
    ];

    private static readonly HashSet<string> _noiseKeys = ["title", "default", "$schema"];

    /// <summary>Builds the parameter schema of a method. Parameters become properties; a cancellation token is skipped.</summary>
    public static JsonObject FromMethod(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var schema = AIJsonUtilities.CreateFunctionJsonSchema(method, serializerOptions: NooaJson.Options);
        return (JsonObject)JsonNode.Parse(schema.GetRawText())!;
    }

    /// <summary>Builds the schema of a type, for structured output.</summary>
    public static JsonObject FromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var schema = AIJsonUtilities.CreateJsonSchema(type, serializerOptions: NooaJson.Options);

        // The schema of object (or any type) is the boolean true: it accepts anything.
        return JsonNode.Parse(schema.GetRawText()) as JsonObject ?? new JsonObject();
    }

    /// <summary>
    /// Cleans a schema for tool use: inlines references, removes title and default noise, and guarantees
    /// the top-level <c>type</c>, <c>properties</c> and <c>required</c>. With <paramref name="strict"/> it also
    /// enforces OpenAI strict mode (<c>additionalProperties: false</c> and all properties required on every object).
    /// </summary>
    public static JsonObject Clean(JsonObject schema, bool strict = false)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var cleaned = StripNoise(ResolveRefs(schema), strict);
        var result = new JsonObject
        {
            ["type"] = cleaned["type"]?.DeepClone() ?? "object",
            ["properties"] = cleaned["properties"]?.DeepClone() ?? new JsonObject(),
            ["required"] = cleaned["required"]?.DeepClone() ?? new JsonArray(),
        };
        if (strict)
        {
            result["additionalProperties"] = false;
        }

        return result;
    }

    /// <summary>
    /// Inlines local <c>#/$defs/</c> references and removes <c>$defs</c>. A recursive or unresolvable reference
    /// becomes a plain object. A single-item <c>allOf</c> is unwrapped.
    /// </summary>
    public static JsonObject ResolveRefs(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        // .NET writes a recursive type as a JSON-pointer reference (#/properties/x), not as a $defs entry,
        // so a schema without $defs can still hold references. They all become plain objects.
        return Inline(schema, schema["$defs"] as JsonObject ?? new JsonObject(), new HashSet<string>());
    }

    /// <summary>
    /// True when every node has a type, every object lists all of its properties as required, and every array has
    /// typed items. A false result means strict mode would be rejected.
    /// </summary>
    public static bool IsStrictValid(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return CheckStrictValid(schema);
    }

    /// <summary>
    /// True when a schema can be expressed in OpenAI strict structured output. Free-form objects, untyped arrays,
    /// tuples, sets and untyped nodes are not compatible. Pass the output of <see cref="ResolveRefs"/>.
    /// </summary>
    public static bool IsStrictCompatible(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return CheckStrictCompatible(schema);
    }

    /// <summary>
    /// Reduces a schema to the keyword subset that OpenAI accepts in a non-strict request. The result guides the
    /// model; the exact type is enforced when the response is parsed.
    /// </summary>
    public static JsonObject Loose(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return (JsonObject)LooseNode(ResolveRefs(schema))!;
    }

    private static JsonObject Inline(JsonObject node, JsonObject defs, IReadOnlySet<string> resolving)
    {
        if (node["$ref"] is JsonValue refValue && refValue.TryGetValue<string>(out var refPath))
        {
            const string prefix = "#/$defs/";
            if (refPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                var name = refPath[prefix.Length..];
                if (resolving.Contains(name))
                {
                    return new JsonObject { ["type"] = "object" };
                }

                if (defs[name] is JsonObject target)
                {
                    var next = new HashSet<string>(resolving) { name };
                    return Inline((JsonObject)target.DeepClone(), defs, next);
                }
            }

            return new JsonObject { ["type"] = "object" };
        }

        var result = new JsonObject();
        foreach (var (key, value) in node)
        {
            switch (key)
            {
                case "$defs":
                    break;
                case "properties" when value is JsonObject props:
                    var inlined = new JsonObject();
                    foreach (var (propName, propValue) in props)
                    {
                        inlined[propName] = propValue is JsonObject po ? Inline(po, defs, resolving) : propValue?.DeepClone();
                    }

                    result[key] = inlined;
                    break;
                case "items" or "additionalProperties" when value is JsonObject child:
                    result[key] = Inline(child, defs, resolving);
                    break;
                case "anyOf" or "oneOf" or "allOf" when value is JsonArray list:
                    result[key] = new JsonArray(list.Select(i => i is JsonObject io ? Inline(io, defs, resolving) : i?.DeepClone()).ToArray());
                    break;
                default:
                    result[key] = value?.DeepClone();
                    break;
            }
        }

        if (result["allOf"] is JsonArray { Count: 1 } single && single[0] is JsonObject only)
        {
            result.Remove("allOf");
            var merged = (JsonObject)only.DeepClone();
            foreach (var (key, value) in result)
            {
                if (!merged.ContainsKey(key))
                {
                    merged[key] = value?.DeepClone();
                }
            }

            result = merged;
        }

        return result;
    }

    private static JsonObject StripNoise(JsonObject node, bool strict)
    {
        var cleaned = new JsonObject();
        foreach (var (key, value) in node)
        {
            if (strict ? _strictAllowed.Contains(key) : !_noiseKeys.Contains(key))
            {
                cleaned[key] = value?.DeepClone();
            }
        }

        if (cleaned["properties"] is JsonObject props)
        {
            cleaned["properties"] = MapObject(props, p => p is JsonObject po ? StripNoise(po, strict) : p);
        }

        foreach (var key in new[] { "items", "additionalProperties" })
        {
            if (cleaned[key] is JsonObject child)
            {
                cleaned[key] = StripNoise(child, strict);
            }
        }

        foreach (var key in _combinators)
        {
            if (cleaned[key] is JsonArray list)
            {
                cleaned[key] = new JsonArray(list.Select(i => i is JsonObject io ? StripNoise(io, strict) : i?.DeepClone()).ToArray());
            }
        }

        if (strict && IsType(cleaned, "object"))
        {
            cleaned["additionalProperties"] = false;
            var properties = cleaned["properties"] as JsonObject ?? new JsonObject();
            cleaned["properties"] = properties;
            cleaned["required"] = new JsonArray(properties.Select(p => (JsonNode)p.Key).ToArray());
        }

        return cleaned;
    }

    private static JsonObject MapObject(JsonObject source, Func<JsonNode?, JsonNode?> map)
    {
        var result = new JsonObject();
        foreach (var (key, value) in source)
        {
            result[key] = map(value)?.DeepClone();
        }

        return result;
    }

    private static bool IsType(JsonObject node, string type) =>
        node["type"] is JsonValue v && v.TryGetValue<string>(out var s) && s == type;

    private static bool HasType(JsonNode? node) => node is JsonObject obj && _typeKeys.Any(obj.ContainsKey);

    private static bool CheckStrictValid(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return true;
        }

        if (!HasCombinatorOrType(obj))
        {
            return false;
        }

        if (IsType(obj, "object") && obj["properties"] is JsonObject properties)
        {
            var required = (obj["required"] as JsonArray)?.Select(r => r?.GetValue<string>()).ToHashSet() ?? [];
            if (!required.SetEquals(properties.Select(p => p.Key)))
            {
                return false;
            }
        }

        if (IsType(obj, "array") && !(obj["items"] is JsonObject items && _typeKeys.Any(items.ContainsKey)))
        {
            return false;
        }

        if (obj["properties"] is JsonObject props && props.Any(p => !CheckStrictValid(p.Value)))
        {
            return false;
        }

        if (!CheckStrictValid(obj["items"]))
        {
            return false;
        }

        return _combinators.All(key => obj[key] is not JsonArray list || list.All(CheckStrictValid));
    }

    // Unlike HasType, enum and const alone do not count here, as upstream.
    private static bool HasCombinatorOrType(JsonObject obj) =>
        obj.ContainsKey("type") || _combinators.Any(obj.ContainsKey);

    private static bool CheckStrictCompatible(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return true;
        }

        if (!HasType(obj))
        {
            return false;
        }

        if (IsType(obj, "object"))
        {
            if (obj["additionalProperties"] is JsonObject || obj["additionalProperties"] is JsonValue { } v && v.TryGetValue<bool>(out var allowed) && allowed)
            {
                return false;
            }

            if (!obj.ContainsKey("properties"))
            {
                return false;
            }
        }

        if (IsType(obj, "array"))
        {
            if (obj.ContainsKey("prefixItems")
                || (obj["uniqueItems"] is JsonValue u && u.TryGetValue<bool>(out var unique) && unique)
                || !HasType(obj["items"]))
            {
                return false;
            }
        }

        if (obj["properties"] is JsonObject props && props.Any(p => !CheckStrictCompatible(p.Value)))
        {
            return false;
        }

        if (obj["items"] is JsonObject items && !CheckStrictCompatible(items))
        {
            return false;
        }

        if (obj["additionalProperties"] is JsonObject extra && !CheckStrictCompatible(extra))
        {
            return false;
        }

        return _combinators.All(key => obj[key] is not JsonArray list || list.All(CheckStrictCompatible));
    }

    private static JsonNode? LooseNode(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return node?.DeepClone();
        }

        var result = new JsonObject();
        foreach (var (key, value) in obj)
        {
            if (_strictAllowed.Contains(key))
            {
                result[key] = value?.DeepClone();
            }
        }

        if (result["properties"] is JsonObject props)
        {
            result["properties"] = MapObject(props, LooseNode);
        }

        foreach (var key in new[] { "items", "additionalProperties" })
        {
            if (result[key] is JsonObject child)
            {
                result[key] = LooseNode(child);
            }
        }

        foreach (var key in _combinators)
        {
            if (result[key] is JsonArray list)
            {
                result[key] = new JsonArray(list.Select(i => i is JsonObject ? LooseNode(i) : i?.DeepClone()).ToArray());
            }
        }

        // Some endpoints reject an object without properties or an array without items, even when not strict.
        if (IsType(result, "object") && !result.ContainsKey("properties"))
        {
            result["properties"] = new JsonObject();
        }

        if (IsType(result, "array") && !result.ContainsKey("items"))
        {
            result["items"] = new JsonObject();
        }

        return result;
    }
}
