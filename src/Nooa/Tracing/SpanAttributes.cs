// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_hooks_impl.py

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Agentdoc;

namespace Nooa.Tracing;

/// <summary>
/// Sets span attributes with the two rules of tracing: every value is scrubbed, and every value has a size bound.
/// </summary>
internal static class SpanAttributes
{
    /// <summary>Largest string attribute, in characters, after scrubbing. A longer value keeps its head and its tail.</summary>
    public const int MaxValueChars = 50_000;

    /// <summary>Largest error message or stack trace, in characters.</summary>
    public const int MaxErrorChars = 5_000;

    private const int _maxScrubInputChars = 1_000_000;
    private const int _cutMargin = 256;
    private const int _maxDepth = 16;
    private const int _maxLength = 20;
    private const int _maxString = 2_000;

    /// <summary>Sets a tag on the span. A string is scrubbed and bounded. A null value is skipped.</summary>
    public static void Set(Activity? span, string key, object? value)
    {
        if (span is null || value is null)
        {
            return;
        }

        if (SecretScrubber.RedactKey(key) is not null)
        {
            Bump(span, 1);
            span.SetTag(key, SecretScrubber.Redacted);
            return;
        }

        if (value is string text)
        {
            var (scrubbed, count) = SecretScrubber.ScrubValue(PreCap(text));
            Bump(span, count);
            span.SetTag(key, Cap((string)scrubbed!, MaxValueChars));
            return;
        }

        var (other, otherCount) = SecretScrubber.ScrubValue(value);
        Bump(span, otherCount);
        span.SetTag(key, other is bool or int or long or double or float or short or byte or uint or ulong or string ? other : Convert.ToString(other, CultureInfo.InvariantCulture));
    }

    /// <summary>Sets the error status and the error attributes. Message and stack trace are bounded.</summary>
    public static void RecordError(Activity? span, Exception exception)
    {
        if (span is null)
        {
            return;
        }

        var type = exception.GetType().Name;
        // Scrub the whole text first, then cap: a cap must never cut a secret in half.
        var message = Cap(SecretScrubber.ScrubString(PreCap(exception.Message)).Text, MaxErrorChars);
        span.SetStatus(ActivityStatusCode.Error, message);
        Set(span, "error.type", type);
        Set(span, "error.message", message);
        Set(span, "exception.type", type);
        Set(span, "exception.message", message);
        Set(span, "exception.stacktrace", Cap(SecretScrubber.ScrubString(PreCap(exception.StackTrace ?? "")).Text, MaxErrorChars));
    }

    /// <summary>Formats a value for an attribute, with the caps of the agent document formatter.</summary>
    public static string SafeSerialize(object? value, int maxChars = MaxValueChars)
    {
        try
        {
            return PFormat.TruncatingFormat(value, maxChars, _maxLength, _maxString, _maxDepth);
        }
        catch (Exception)
        {
            return "<unserializable>";
        }
    }

    /// <summary>
    /// Serializes a set of named values to valid JSON. A value that JSON cannot hold becomes its bounded text form.
    /// When the result is too long, each value is bounded, so the top-level keys stay.
    /// </summary>
    public static string SafeJson(IEnumerable<KeyValuePair<string, object?>> values, int maxChars = MaxValueChars)
    {
        var list = values.ToList();
        var encoded = Encode(list, maxChars);
        if (encoded.Length <= maxChars)
        {
            return encoded;
        }

        var perValue = Math.Max(256, maxChars / (4 * Math.Max(1, list.Count)));
        var shaped = Encode(list.Select(p => new KeyValuePair<string, object?>(p.Key, SafeSerialize(p.Value, perValue))).ToList(), perValue);
        return shaped.Length <= maxChars ? shaped : JsonSerializer.Serialize(SafeSerialize(list, maxChars));
    }

    /// <summary>Returns the text unchanged when it fits, else its head and its tail with a notice.</summary>
    public static string Cap(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        var half = Math.Max(1, (maxChars - 64) / 2);
        return string.Concat(text.AsSpan(0, half), $"\n... [{text.Length - (2 * half)} characters omitted] ...\n", text.AsSpan(text.Length - half));
    }

    // Bounds the text that the scrubber scans. The cut drops a margin on each side, so no secret of ordinary length is left half-cut.
    private static string PreCap(string text)
    {
        if (text.Length <= _maxScrubInputChars)
        {
            return text;
        }

        var keep = (_maxScrubInputChars / 2) - _cutMargin;
        return string.Concat(text.AsSpan(0, keep), $"\n... [{text.Length - (2 * keep)} characters omitted] ...\n", text.AsSpan(text.Length - keep));
    }

    private static string Encode(List<KeyValuePair<string, object?>> values, int perValue)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values)
        {
            obj[key] = ToNode(value, perValue);
        }

        return obj.ToJsonString();
    }

    private static JsonNode? ToNode(object? value, int maxChars) => value switch
    {
        null => null,
        string s => JsonValue.Create(Cap(s, maxChars)),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d when double.IsFinite(d) => JsonValue.Create(d),
        float f when float.IsFinite(f) => JsonValue.Create(f),
        IDictionary<string, object?> map => new JsonObject(map.Select(p => new KeyValuePair<string, JsonNode?>(p.Key, ToNode(p.Value, maxChars)))),
        object?[] array => new JsonArray([.. array.Select(v => ToNode(v, maxChars))]),
        _ => JsonValue.Create(SafeSerialize(value, maxChars)),
    };

    private static void Bump(Activity span, int count)
    {
        if (count == 0)
        {
            return;
        }

        var previous = span.GetTagItem(OpenInferenceAttributes.SecretsRedactedCount) is int n ? n : 0;
        span.SetTag(OpenInferenceAttributes.SecretsRedactedCount, previous + count);
    }
}
