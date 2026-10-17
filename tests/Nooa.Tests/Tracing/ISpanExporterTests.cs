// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

/// <summary>Contract tests that every built-in exporter must pass.</summary>
[Collection(AgentStaticCollection.Name)]
public class ISpanExporterTests
{
    public static TheoryData<string> Kinds => new() { "jsonl", "otlp" };

    private static (ISpanExporter Exporter, Action Cleanup) Create(string kind)
    {
        if (kind == "jsonl")
        {
            var dir = new TempDirectory();
            return (new JsonlSpanExporter(dir.Path), dir.Dispose);
        }

        return (new OtlpHttpSpanExporter("http://localhost:5001/v1/traces", new HttpClient(new RecordingHttpHandler())), () => { });
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Contract_ExportFlushShutdown_Succeed(string kind)
    {
        using var capture = new SpanCapture();
        var (exporter, cleanup) = Create(kind);
        var span = NooaTracing.Source.StartActivity("s")!;
        span.Stop();

        Assert.True(exporter.Export([span], new Dictionary<string, object?>()));
        Assert.True(exporter.Flush(TimeSpan.FromSeconds(10)));
        Assert.False(string.IsNullOrWhiteSpace(exporter.Describe()));
        exporter.Shutdown();
        cleanup();
    }
}
