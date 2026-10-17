// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_idempotent_tracing.py, test_exporter_isolation.py and test_processor_assignment.py

#pragma warning disable xUnit1051 // The agentic methods take an optional token that these tests do not need.

using System.Diagnostics;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Tests.Runtime;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public sealed class NooaTracingTests : IDisposable
{
    public NooaTracingTests() => NooaTracing.Shutdown();

    public void Dispose() => NooaTracing.Shutdown();

    private static EchoAgent Agent(string reply = "ok") => new(new AgentOptions { Llm = FakeLlmClient.SimpleMessage(reply), Strategies = [] });

    [Fact]
    public async Task Enable_InstallsHooksAndExportsSpansWithTheSession()
    {
        var exporter = new RecordingSpanExporter();

        NooaTracing.Enable(exporter);
        await Agent().AskAsync("q");

        Assert.True(NooaTracing.IsEnabled);
        Assert.IsType<OpenInferenceHooks>(InstrumentationHooks.Current);
        Assert.Equal(["generation", "method.AskAsync"], exporter.Spans.Select(s => s.DisplayName));
        Assert.All(exporter.Spans, s => Assert.Matches("^[0-9]{8}_[0-9]{6}_[0-9a-f]{8}$", (string)s.GetTagItem("session.id")!));
        Assert.Equal("nooa", exporter.Resource!["service.name"]);
    }

    [Fact]
    public async Task Enable_ScrubsTagsThatOtherCodeAdds()
    {
        var exporter = new RecordingSpanExporter();
        NooaTracing.Enable(exporter);

        using (var span = NooaTracing.Source.StartActivity("custom")!)
        {
            span.SetTag("note", "key AKIAIOSFODNN7EXAMPLE");
            span.SetTag("authorization", "Bearer abc");
            span.SetTag("json", "{\"password\":\"p\"}");
            span.SetTag("count", 3);
        }

        await Task.CompletedTask;
        var recorded = exporter.Spans.Single();
        Assert.Equal("key [REDACTED]", recorded.GetTagItem("note"));
        Assert.Equal("[REDACTED]", recorded.GetTagItem("authorization"));
        Assert.Equal("{\"password\":\"[REDACTED]\"}", recorded.GetTagItem("json"));
        Assert.Equal(3, recorded.GetTagItem("count"));
        Assert.Equal(3, recorded.GetTagItem("secrets.redacted_count"));
    }

    [Fact]
    public void Enable_WhenOtherHooksAreInstalled_Throws()
    {
        InstrumentationHooks.Current = new RecordingHooks();
        try
        {
            Assert.Throws<InvalidOperationException>(() => NooaTracing.Enable(new RecordingSpanExporter()));
            Assert.False(NooaTracing.IsEnabled);
        }
        finally
        {
            InstrumentationHooks.Current = null;
        }
    }

    [Fact]
    public async Task Enable_Twice_ReplacesTheExportersAndShutsDownTheOldOnes()
    {
        var first = new RecordingSpanExporter();
        var second = new RecordingSpanExporter();
        NooaTracing.Enable(first);
        var hooks = InstrumentationHooks.Current;

        NooaTracing.Enable(second);
        await Agent().AskAsync("q");

        Assert.True(first.IsShutDown);
        Assert.Empty(first.Spans);
        Assert.Equal(2, second.Spans.Count);
        Assert.Same(hooks, InstrumentationHooks.Current);
    }

    [Fact]
    public async Task Shutdown_RemovesTheHooksAndStopsExporting()
    {
        var exporter = new RecordingSpanExporter();
        NooaTracing.Enable(exporter);

        NooaTracing.Shutdown();
        await Agent().AskAsync("q");

        Assert.False(NooaTracing.IsEnabled);
        Assert.Null(InstrumentationHooks.Current);
        Assert.True(exporter.IsShutDown);
        Assert.Empty(exporter.Spans);
        NooaTracing.Shutdown();
    }

    [Fact]
    public async Task Exporter_ThatThrows_DoesNotAffectTheAgentOrTheOtherExporters()
    {
        var failing = new RecordingSpanExporter { Throw = true };
        var good = new RecordingSpanExporter();
        NooaTracing.Enable(failing, good);

        Assert.Equal("ok", await Agent().AskAsync("q"));

        Assert.Equal(2, good.Spans.Count);
    }

    [Fact]
    public void Enable_ResourceHasMetadataExperimentAndScrubbedExtras()
    {
        var exporter = new RecordingSpanExporter();

        NooaTracing.Enable(
            [exporter],
            "exp-1",
            new Dictionary<string, object?> { ["eval.model"] = "m", ["api_key"] = "abc", ["note"] = "AKIAIOSFODNN7EXAMPLE", ["skip"] = null },
            logger: null);
        using (NooaTracing.Source.StartActivity("x"))
        {
        }

        var resource = exporter.Resource!;
        Assert.Equal("exp-1", resource["experiment"]);
        Assert.Equal("m", resource["eval.model"]);
        Assert.Equal("[REDACTED]", resource["api_key"]);
        Assert.Equal("[REDACTED]", resource["note"]);
        Assert.False(resource.ContainsKey("skip"));
        Assert.True(resource.ContainsKey("nooa.version"));
        Assert.True(resource.ContainsKey("dotnet.version"));
    }

    [Fact]
    public void Enable_WithoutArguments_WritesJsonlToTraceDir()
    {
        using var dir = new TempDirectory();
        using var env = new EnvVars();
        env.Set("TRACE_DIR", dir.Path);

        NooaTracing.Enable();
        using (var session = TraceSession.Scope("default-run"))
        using (var span = NooaTracing.Source.StartActivity("x")!)
        {
            span.SetTag("session.id", "default-run");
        }

        Assert.True(File.Exists(dir.File("default-run.jsonl")));
    }

    [Fact]
    public void Flush_WaitsForTheExporters()
    {
        NooaTracing.Enable(new RecordingSpanExporter());

        Assert.True(NooaTracing.Flush());
        NooaTracing.Shutdown();
        Assert.True(NooaTracing.Flush(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void EndActiveSpans_EndsAnOpenCall()
    {
        var exporter = new RecordingSpanExporter();
        NooaTracing.Enable(exporter);
        InstrumentationHooks.Current!.BeforeAgentCall(Agent(), "AskAsync", [], "call-1", null);

        Assert.Equal(1, NooaTracing.EndActiveSpans("cancelled by user"));

        var span = Assert.Single(exporter.Spans);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("Cancelled", span.GetTagItem("error.type"));
    }

    [Fact]
    public void EndActiveSpans_WhenDisabled_ReturnsZero() =>
        Assert.Equal(0, NooaTracing.EndActiveSpans());

    [Fact]
    public void Source_IsNamedNooa() =>
        Assert.Equal("Nooa", NooaTracing.Source.Name);
}
