// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_exporters.py

using System.Diagnostics;
using System.Text.Json.Nodes;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class OtlpJsonTests
{
    private static readonly Dictionary<string, object?> _resource = new() { ["service.name"] = "nooa" };

    private static JsonObject Serialize(params Activity[] spans) =>
        JsonNode.Parse(OtlpJson.Serialize(spans, _resource))!.AsObject();

    [Fact]
    public void Serialize_Span_HasTheOtlpTracesDataShape()
    {
        using var capture = new SpanCapture();
        var parent = NooaTracing.Source.StartActivity("parent")!;
        var child = NooaTracing.Source.StartActivity("child", ActivityKind.Client)!;
        child.SetTag("s", "text");
        child.SetTag("i", 7);
        child.SetTag("d", 1.5);
        child.SetTag("b", true);
        child.SetTag("null", null);
        child.AddEvent(new ActivityEvent("evt", tags: new ActivityTagsCollection { ["k"] = "v" }));
        child.SetStatus(ActivityStatusCode.Error, "bad");
        child.Stop();
        parent.Stop();

        var root = Serialize(child);

        var resourceSpans = root["resourceSpans"]!.AsArray().Single()!;
        Assert.Equal("service.name", (string)resourceSpans["resource"]!["attributes"]![0]!["key"]!);
        var scope = resourceSpans["scopeSpans"]!.AsArray().Single()!;
        Assert.Equal("Nooa", (string)scope["scope"]!["name"]!);
        var span = scope["spans"]!.AsArray().Single()!;
        Assert.Equal(32, ((string)span["traceId"]!).Length);
        Assert.Equal(16, ((string)span["spanId"]!).Length);
        Assert.Equal(parent.SpanId.ToHexString(), (string)span["parentSpanId"]!);
        Assert.Equal("child", (string)span["name"]!);
        Assert.Equal(3, (int)span["kind"]!);
        Assert.Equal(2, (int)span["status"]!["code"]!);
        Assert.Equal("bad", (string)span["status"]!["message"]!);
        Assert.True(long.Parse((string)span["endTimeUnixNano"]!) >= long.Parse((string)span["startTimeUnixNano"]!));
        var attributes = span["attributes"]!.AsArray().ToDictionary(a => (string)a!["key"]!, a => a!["value"]!);
        Assert.Equal("text", (string)attributes["s"]["stringValue"]!);
        Assert.Equal("7", (string)attributes["i"]["intValue"]!);
        Assert.Equal(1.5, (double)attributes["d"]["doubleValue"]!);
        Assert.True((bool)attributes["b"]["boolValue"]!);
        Assert.DoesNotContain("null", attributes.Keys);
        Assert.Equal("evt", (string)span["events"]![0]!["name"]!);
    }

    [Fact]
    public void Serialize_RootSpan_HasNoParentSpanId()
    {
        using var capture = new SpanCapture();
        var span = NooaTracing.Source.StartActivity("root")!;
        span.Stop();

        var json = Serialize(span)["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]![0]!.AsObject();

        Assert.False(json.ContainsKey("parentSpanId"));
        Assert.False(json.ContainsKey("events"));
    }

    [Fact]
    public void Serialize_NonAsciiText_IsKeptAsLiteralCharacters()
    {
        using var capture = new SpanCapture();
        var span = NooaTracing.Source.StartActivity("root")!;
        span.SetTag("t", "héllo 世界");
        span.Stop();

        var text = System.Text.Encoding.UTF8.GetString(OtlpJson.Serialize([span], _resource));

        Assert.Contains("héllo 世界", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', text);
    }

    private sealed class Leaky : IFormattable
    {
        public override string ToString() => "obj AKIAIOSFODNN7EXAMPLE";

        public string ToString(string? format, IFormatProvider? formatProvider) => ToString();
    }

    [Fact]
    public void Serialize_SecretsOnAnyPath_AreScrubbedAtTheWire()
    {
        using var capture = new SpanCapture();
        var span = NooaTracing.Source.StartActivity("name AKIAIOSFODNN7EXAMPLE")!;
        span.SetTag("object", new Leaky());
        span.SetTag("AKIAIOSFODNN7EXAMPLE", "v");
        span.SetTag("password", 12345);
        span.AddEvent(new ActivityEvent("evt ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmn", tags: new ActivityTagsCollection { ["note"] = "AKIAIOSFODNN7EXAMPLE", ["token"] = "x" }));
        span.SetStatus(ActivityStatusCode.Error, "failed with AKIAIOSFODNN7EXAMPLE");
        span.Stop();

        var text = System.Text.Encoding.UTF8.GetString(OtlpJson.Serialize([span], new Dictionary<string, object?> { ["leak"] = "AKIAIOSFODNN7EXAMPLE", ["api_key"] = "z" }));

        Assert.DoesNotContain("AKIA", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_", text, StringComparison.Ordinal);
        Assert.DoesNotContain("12345", text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
    }
}
