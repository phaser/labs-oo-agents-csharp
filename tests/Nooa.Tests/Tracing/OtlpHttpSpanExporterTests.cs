// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_http_exporter_drops.py

using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class OtlpHttpSpanExporterTests
{
    private static readonly Dictionary<string, object?> _resource = new() { ["service.name"] = "nooa" };

    private static Activity Span(string? session)
    {
        var span = NooaTracing.Source.StartActivity("s")!;
        span.SetTag("session.id", session);
        span.Stop();
        return span;
    }

    [Fact]
    public void Export_PostsOtlpJsonGroupedBySession()
    {
        using var capture = new SpanCapture();
        using var env = new EnvVars();
        env.Set("NOOA_VIEWER_AUTH_TOKEN", null);
        var handler = new RecordingHttpHandler();
        var exporter = new OtlpHttpSpanExporter("http://localhost:5001/v1/traces", new HttpClient(handler));

        exporter.Export([Span("a"), Span("b"), Span("a")], _resource);
        Assert.True(exporter.Flush(TimeSpan.FromSeconds(10)));
        exporter.Shutdown();

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal("application/json", r.ContentType);
            Assert.Equal("http://localhost:5001/v1/traces", r.Uri!.ToString());
            Assert.Null(r.Authorization);
        });
        var sessions = handler.Requests.Select(r => SessionOf(r.Body)).Order().ToList();
        Assert.Equal(["a", "b"], sessions);
        Assert.Equal(3, handler.Requests.Select(r => JsonNode.Parse(r.Body)!).Sum(j => j["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]!.AsArray().Count));
    }

    private static string SessionOf(string body) =>
        JsonNode.Parse(body)!["resourceSpans"]![0]!["resource"]!["attributes"]!.AsArray()
            .Single(a => (string)a!["key"]! == "session.id")!["value"]!["stringValue"]!.ToString();

    [Fact]
    public void Export_FailedRequest_CountsTheDroppedSpans()
    {
        using var capture = new SpanCapture();
        var handler = new RecordingHttpHandler(HttpStatusCode.InternalServerError);
        var exporter = new OtlpHttpSpanExporter("http://localhost:5001/v1/traces", new HttpClient(handler));

        exporter.Export([Span("a"), Span("a")], _resource);
        exporter.Flush(TimeSpan.FromSeconds(10));

        Assert.Equal(2, exporter.DroppedSpans);
        exporter.Shutdown();
    }

    [Fact]
    public void Export_QueueFull_DropsAndReportsFailure()
    {
        using var capture = new SpanCapture();
        var gate = new ManualResetEventSlim(false);
        var exporter = new OtlpHttpSpanExporter("http://localhost:5001/v1/traces", new HttpClient(new BlockingHandler(gate)));
        var span = Span("a");
        var ok = true;

        for (var i = 0; i < OtlpHttpSpanExporter.QueueCapacity + 2000 && ok; i++)
        {
            ok = exporter.Export([span], _resource);
        }

        Assert.False(ok);
        Assert.True(exporter.DroppedSpans > 0);
        gate.Set();
        exporter.Shutdown();
    }

    [Fact]
    public void Export_BearerToken_IsSentToLoopbackAndHttps()
    {
        using var capture = new SpanCapture();
        using var env = new EnvVars();
        env.Set("NOOA_VIEWER_AUTH_TOKEN", "  viewer-token-1234  ");
        var local = new RecordingHttpHandler();
        var remote = new RecordingHttpHandler();
        var localExporter = new OtlpHttpSpanExporter("http://127.0.0.1:5001/v1/traces", new HttpClient(local));
        var remoteExporter = new OtlpHttpSpanExporter("https://viewer.example.com/v1/traces", new HttpClient(remote));

        localExporter.Export([Span("a")], _resource);
        remoteExporter.Export([Span("a")], _resource);
        localExporter.Flush(TimeSpan.FromSeconds(10));
        remoteExporter.Flush(TimeSpan.FromSeconds(10));

        Assert.Equal("Bearer viewer-token-1234", local.Requests.Single().Authorization);
        Assert.Equal("Bearer viewer-token-1234", remote.Requests.Single().Authorization);
        localExporter.Shutdown();
        remoteExporter.Shutdown();
    }

    [Fact]
    public void Export_BearerToken_IsNotSentOverPlainHttpToAnotherHost()
    {
        using var capture = new SpanCapture();
        using var env = new EnvVars();
        env.Set("NOOA_VIEWER_AUTH_TOKEN", "viewer-token-1234");
        var handler = new RecordingHttpHandler();
        var exporter = new OtlpHttpSpanExporter("http://viewer.example.com/v1/traces", new HttpClient(handler));

        exporter.Export([Span("a")], _resource);
        exporter.Flush(TimeSpan.FromSeconds(10));

        Assert.Null(handler.Requests.Single().Authorization);
        exporter.Shutdown();
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://host/x")]
    [InlineData("/relative")]
    [InlineData("http://user:pass@host/x")]
    public void Constructor_InvalidEndpoint_Throws(string endpoint) =>
        Assert.Throws<ArgumentException>(() => new OtlpHttpSpanExporter(endpoint));

    [Fact]
    public void Constructor_WithoutEndpoint_ReadsOtlpEndpointOrTheLocalViewer()
    {
        using var env = new EnvVars();
        env.Set("OTLP_ENDPOINT", "http://collector.example.com:4318/v1/traces?x=1");
        var fromEnv = new OtlpHttpSpanExporter(client: new HttpClient(new RecordingHttpHandler()));
        env.Set("OTLP_ENDPOINT", null);
        var fallback = new OtlpHttpSpanExporter(client: new HttpClient(new RecordingHttpHandler()));

        Assert.Equal("otlp:http://collector.example.com:4318/v1/traces", fromEnv.Describe());
        Assert.Equal("otlp:http://localhost:5001/v1/traces", fallback.Describe());
        fromEnv.Shutdown();
        fallback.Shutdown();
    }

    [Fact]
    public void Export_UnexpectedHandlerException_DropsTheBatchAndKeepsTheWorkerAlive()
    {
        using var capture = new SpanCapture();
        var handler = new ThrowingOnceHandler();
        var exporter = new OtlpHttpSpanExporter("http://localhost:5001/v1/traces", new HttpClient(handler));

        exporter.Export([Span("a")], _resource);
        Assert.True(exporter.Flush(TimeSpan.FromSeconds(10)));
        exporter.Export([Span("a")], _resource);
        Assert.True(exporter.Flush(TimeSpan.FromSeconds(10)));
        exporter.Shutdown();

        Assert.Equal(1, exporter.DroppedSpans);
        Assert.Equal(2, handler.Calls);
    }

    private sealed class ThrowingOnceHandler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _calls) == 1
                ? throw new NotSupportedException("unexpected")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class BlockingHandler(ManualResetEventSlim gate) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            gate.Wait(cancellationToken);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
