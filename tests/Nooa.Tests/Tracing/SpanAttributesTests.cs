// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_safe_serialize.py and test_exception_stacktrace.py

using System.Diagnostics;
using System.Text.Json.Nodes;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class SpanAttributesTests
{
    private static Activity Start(SpanCapture capture) => NooaTracing.Source.StartActivity("t")!;

    [Fact]
    public void Set_SecretInString_IsRedactedAndCounted()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);

        SpanAttributes.Set(span, "x", "key AKIAIOSFODNN7EXAMPLE");

        Assert.Equal("key [REDACTED]", span.GetTagItem("x"));
        Assert.Equal(1, span.GetTagItem("secrets.redacted_count"));
    }

    [Fact]
    public void Set_CredentialKey_RedactsAnyValue()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);

        SpanAttributes.Set(span, "http.request.header.authorization", "short");
        SpanAttributes.Set(span, "http.request.header.authorization", 12345);

        Assert.Equal("[REDACTED]", span.GetTagItem("http.request.header.authorization"));
        Assert.Equal(2, span.GetTagItem("secrets.redacted_count"));
    }

    [Fact]
    public void Set_NullValue_SetsNothing()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);

        SpanAttributes.Set(span, "x", null);
        SpanAttributes.Set(null, "x", "y");

        Assert.Null(span.GetTagItem("x"));
    }

    [Fact]
    public void Set_HugeString_KeepsHeadAndTailWithinTheBound()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);

        SpanAttributes.Set(span, "x", "HEAD" + new string('m', 200_000) + "TAIL");

        var value = (string)span.GetTagItem("x")!;
        Assert.True(value.Length <= SpanAttributes.MaxValueChars);
        Assert.StartsWith("HEAD", value, StringComparison.Ordinal);
        Assert.EndsWith("TAIL", value, StringComparison.Ordinal);
        Assert.Contains("characters omitted", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_SecretNearTheCutPoint_IsScrubbedBeforeTruncation()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);
        var secret = "AKIAIOSFODNN7EXAMPLE";

        SpanAttributes.Set(span, "x", new string('m', SpanAttributes.MaxValueChars / 2 - 40) + secret + new string('m', 200_000));

        Assert.DoesNotContain("AKIA", (string)span.GetTagItem("x")!, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_NonPrimitive_BecomesText()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);

        SpanAttributes.Set(span, "x", new Uri("http://example.com/"));
        SpanAttributes.Set(span, "n", 5L);

        Assert.Equal("http://example.com/", span.GetTagItem("x"));
        Assert.Equal(5L, span.GetTagItem("n"));
    }

    [Fact]
    public void RecordError_SetsStatusAndBoundedAttributes()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);

        SpanAttributes.RecordError(span, new InvalidOperationException(new string('e', 20_000) + " api_key=hunter2"));

        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("InvalidOperationException", span.GetTagItem("error.type"));
        Assert.True(((string)span.GetTagItem("error.message")!).Length <= SpanAttributes.MaxErrorChars);
        Assert.True(span.StatusDescription!.Length <= SpanAttributes.MaxErrorChars);
        Assert.DoesNotContain("hunter2", span.StatusDescription, StringComparison.Ordinal);
        Assert.NotNull(span.GetTagItem("exception.stacktrace"));
    }

    [Fact]
    public void RecordError_StackTrace_IsIncluded()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);
        Exception thrown;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            thrown = ex;
        }

        SpanAttributes.RecordError(span, thrown);

        Assert.Contains(nameof(RecordError_StackTrace_IsIncluded), (string)span.GetTagItem("exception.stacktrace")!, StringComparison.Ordinal);
    }

    [Fact]
    public void SafeSerialize_Object_UsesTheBoundedFormatter()
    {
        var text = SpanAttributes.SafeSerialize(Enumerable.Range(0, 1000).ToList(), maxChars: 200);

        Assert.True(text.Length <= 400);
    }

    [Fact]
    public void SafeSerialize_String_IsReturnedAsIs() =>
        Assert.Equal("hello", SpanAttributes.SafeSerialize("hello"));

    [Fact]
    public void SafeJson_SmallValues_AreValidJson()
    {
        var json = SpanAttributes.SafeJson([new("a", 1), new("b", "x"), new("c", true), new("d", null), new("e", new Uri("http://x/"))]);

        var node = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(1, (int)node["a"]!);
        Assert.Equal("x", (string)node["b"]!);
        Assert.True((bool)node["c"]!);
        Assert.Null(node["d"]);
        Assert.Equal("http://x/", (string)node["e"]!);
    }

    [Fact]
    public void SafeJson_Overflow_KeepsTheTopLevelKeys()
    {
        var json = SpanAttributes.SafeJson([new("args", new string('z', 100_000)), new("kwargs", "k")], maxChars: 5000);

        var node = JsonNode.Parse(json)!.AsObject();
        Assert.True(json.Length <= 5000);
        Assert.Contains("args", node.Select(p => p.Key));
        Assert.Contains("kwargs", node.Select(p => p.Key));
    }

    [Fact]
    public void Cap_ShortText_IsUnchanged() =>
        Assert.Equal("abc", SpanAttributes.Cap("abc", 10));

    [Fact]
    public void RecordError_SecretAcrossTheCapBoundary_IsRedacted()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);
        var message = new string('m', SpanAttributes.MaxErrorChars - 10) + "AKIAIOSFODNN7EXAMPLE" + new string('m', 50);

        SpanAttributes.RecordError(span, new InvalidOperationException(message));

        var all = string.Concat(span.TagObjects.Select(t => t.Value?.ToString())) + span.StatusDescription;
        Assert.DoesNotContain("AKIA", all, StringComparison.Ordinal);
        Assert.DoesNotContain("IOSFODNN", all, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_SecretAcrossThePreCapBoundary_LeavesNoFragment()
    {
        using var capture = new SpanCapture();
        using var span = Start(capture);
        var secret = "AKIAIOSFODNN7EXAMPLE";

        SpanAttributes.Set(span, "x", new string('m', 500_000 - 10) + secret + new string('m', 600_000));

        Assert.DoesNotContain("AKIA", (string)span.GetTagItem("x")!, StringComparison.Ordinal);
        Assert.DoesNotContain("IOSFODNN", (string)span.GetTagItem("x")!, StringComparison.Ordinal);
    }
}
