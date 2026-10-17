// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_otlp_serialize.py

using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Nooa.Tracing;

/// <summary>
/// Converts finished spans to the OTLP JSON wire format (<c>TracesData</c>, the <c>ExportTraceServiceRequest</c> schema).
/// </summary>
internal static class OtlpJson
{
    private static readonly JsonWriterOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Returns one <c>TracesData</c> object as UTF-8 JSON, with no line break.</summary>
    public static byte[] Serialize(IReadOnlyList<Activity> spans, IReadOnlyDictionary<string, object?> resource)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, _options))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("resourceSpans");
            writer.WriteStartObject();
            writer.WriteStartObject("resource");
            WriteAttributes(writer, resource);
            writer.WriteEndObject();
            writer.WriteStartArray("scopeSpans");
            foreach (var group in spans.GroupBy(s => (s.Source.Name, s.Source.Version)))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("scope");
                writer.WriteString("name", group.Key.Name);
                if (!string.IsNullOrEmpty(group.Key.Version))
                {
                    writer.WriteString("version", group.Key.Version);
                }

                writer.WriteEndObject();
                writer.WriteStartArray("spans");
                foreach (var span in group)
                {
                    WriteSpan(writer, span);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteSpan(Utf8JsonWriter writer, Activity span)
    {
        writer.WriteStartObject();
        writer.WriteString("traceId", span.TraceId.ToHexString());
        writer.WriteString("spanId", span.SpanId.ToHexString());
        if (span.ParentSpanId != default)
        {
            writer.WriteString("parentSpanId", span.ParentSpanId.ToHexString());
        }

        writer.WriteString("name", Clean(span.DisplayName));
        writer.WriteNumber("kind", (int)span.Kind + 1);
        writer.WriteString("startTimeUnixNano", Nanos(span.StartTimeUtc).ToString(CultureInfo.InvariantCulture));
        writer.WriteString("endTimeUnixNano", Nanos(span.StartTimeUtc + span.Duration).ToString(CultureInfo.InvariantCulture));
        WriteAttributes(writer, span.TagObjects.Select(t => new KeyValuePair<string, object?>(t.Key, t.Value)));
        writer.WriteStartObject("status");
        writer.WriteNumber("code", (int)span.Status);
        if (!string.IsNullOrEmpty(span.StatusDescription))
        {
            writer.WriteString("message", Clean(span.StatusDescription));
        }

        writer.WriteEndObject();
        if (span.Events.Any())
        {
            writer.WriteStartArray("events");
            foreach (var e in span.Events)
            {
                writer.WriteStartObject();
                writer.WriteString("timeUnixNano", Nanos(e.Timestamp.UtcDateTime).ToString(CultureInfo.InvariantCulture));
                writer.WriteString("name", Clean(e.Name));
                WriteAttributes(writer, e.Tags);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        writer.WriteStartArray("attributes");
        foreach (var (key, value) in attributes)
        {
            if (value is null)
            {
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("key", Clean(key));
            writer.WriteStartObject("value");
            if (SecretScrubber.RedactKey(key) is not null)
            {
                writer.WriteString("stringValue", SecretScrubber.Redacted);
            }
            else
            {
                switch (value)
                {
                    case bool b:
                        writer.WriteBoolean("boolValue", b);
                        break;
                    case int or long or short or byte or uint or ulong:
                        writer.WriteString("intValue", Convert.ToString(value, CultureInfo.InvariantCulture));
                        break;
                    case double d when double.IsFinite(d):
                        writer.WriteNumber("doubleValue", d);
                        break;
                    case float f when float.IsFinite(f):
                        writer.WriteNumber("doubleValue", f);
                        break;
                    default:
                        // Every other value, strings included, goes through the scrubber at the last step before the wire.
                        var (clean, _) = SecretScrubber.ScrubValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                        writer.WriteString("stringValue", (string)clean!);
                        break;
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string Clean(string text) => SecretScrubber.ScrubString(text).Text;

    private static long Nanos(DateTime utc) => (utc - DateTime.UnixEpoch).Ticks * 100;
}
