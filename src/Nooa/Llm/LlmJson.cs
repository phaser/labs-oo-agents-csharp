// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Nooa.Llm;

/// <summary>Tolerant JSON extraction from model text.</summary>
public static partial class LlmJson
{
    private const int _previewLength = 500;
    private const int _regexTimeoutMs = 1000;

    /// <summary>
    /// Parses JSON from model output. It removes a full-response code fence and markdown emphasis markers,
    /// extracts an object that is embedded in prose, removes control characters, repairs bad escapes, and
    /// decodes nested JSON that a model encoded as a string.
    /// </summary>
    /// <exception cref="JsonException">The text holds no parseable JSON.</exception>
    public static JsonNode? ExtractAndParse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            return ExtractAndParseCore(text);
        }
        catch (RegexMatchTimeoutException e)
        {
            // The text is model output. A pattern that backtracks for too long is a parse failure, not a hang.
            throw new JsonException("The text took too long to scan for JSON", e);
        }
    }

    private static JsonNode? ExtractAndParseCore(string text)
    {
        var original = text;
        text = text.Trim();

        var fence = FencePattern().Match(text);
        if (fence.Success)
        {
            text = fence.Groups[1].Value.Trim();
        }

        text = LeadingMarkers().Replace(text, "");
        text = TrailingMarkers().Replace(text, "");

        if (text.Length == 0)
        {
            throw new JsonException($"Empty text after processing. Original: `{Truncate(original, 200)}` ...");
        }

        if (TryParse(text, out var parsed, out var firstError))
        {
            return DecodeNested(parsed);
        }

        if (text.Contains("[...]", StringComparison.Ordinal)
            || text.Contains("\"...\"", StringComparison.Ordinal)
            || text.Contains(": ...", StringComparison.Ordinal))
        {
            throw new JsonException(
                "JSON contains abbreviations/ellipsis ([...] or \"...\" or ': ...'). "
                + "You MUST provide the complete, unabbreviated JSON. Do not truncate or use placeholders. "
                + "Write out ALL values in full.",
                innerException: firstError);
        }

        var nested = NestedObject().Match(text);
        if (nested.Success && TryParse(nested.Value, out parsed, out _))
        {
            return DecodeNested(parsed);
        }

        text = ControlChars().Replace(text, "");
        text = BadEscape().Replace(text, "\\\\");

        if (TryParse(text, out parsed, out var lastError))
        {
            return DecodeNested(parsed);
        }

        throw new JsonException(
            $"Failed to parse JSON after multiple cleanup attempts. Text preview: {Truncate(text, _previewLength)}",
            innerException: lastError);
    }

    [GeneratedRegex(@"^```(?:json)?[ \t]*\r?\n(.*?)\r?\n?```$", RegexOptions.Singleline, _regexTimeoutMs)]
    private static partial Regex FencePattern();

    [GeneratedRegex(@"^\*{1,3}\s*", RegexOptions.None, _regexTimeoutMs)]
    private static partial Regex LeadingMarkers();

    [GeneratedRegex(@"\s*\*{1,3}$", RegexOptions.None, _regexTimeoutMs)]
    private static partial Regex TrailingMarkers();

    [GeneratedRegex(@"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}", RegexOptions.Singleline, _regexTimeoutMs)]
    private static partial Regex NestedObject();

    [GeneratedRegex(@"[\x00-\x1f\x7f-\x9f]", RegexOptions.None, _regexTimeoutMs)]
    private static partial Regex ControlChars();

    [GeneratedRegex(@"\\(?![""\\/bfnrt]|u[0-9a-fA-F]{4})", RegexOptions.None, _regexTimeoutMs)]
    private static partial Regex BadEscape();

    private static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;

    private static bool TryParse(string text, out JsonNode? node, out JsonException? error)
    {
        try
        {
            node = JsonNode.Parse(text);
            error = null;
            return true;
        }
        catch (JsonException e)
        {
            node = null;
            error = e;
            return false;
        }
    }

    private static JsonNode? DecodeNested(JsonNode? node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var s):
                var stripped = s.Trim();
                if ((stripped.StartsWith('{') || stripped.StartsWith('[')) && TryParse(stripped, out var inner, out _))
                {
                    return DecodeNested(inner);
                }

                return node;
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var (key, child) in obj)
                {
                    result[key] = DecodeNested(child?.DeepClone());
                }

                return result;
            case JsonArray array:
                return new JsonArray(array.Select(i => DecodeNested(i?.DeepClone())).ToArray());
            default:
                return node;
        }
    }
}
