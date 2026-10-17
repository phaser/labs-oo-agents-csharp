// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/06_tracing.py

#pragma warning disable xUnit1051 // The agentic methods take an optional token that these tests do not need.

using System.Text.Json.Nodes;
using Nooa.Llm;
using Nooa.Strategies;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

/// <summary>Offline acceptance test for quickstart 06: all method types are traced with parent-child spans into a JSONL trace file.</summary>
[Collection(AgentStaticCollection.Name)]
public sealed class Quickstart06TracingTests : IDisposable
{
    public Quickstart06TracingTests() => NooaTracing.Shutdown();

    public void Dispose() => NooaTracing.Shutdown();

    private static MathAgent CreateAgent(FakeLlmClient fake) => new(new AgentOptions
    {
        Llm = new TracingLlmClient(fake),
        DefaultStrategy = new PredictStrategy(),
    });

    private static List<JsonNode> ReadSpans(string file) =>
        [.. File.ReadAllLines(file)
            .SelectMany(line => JsonNode.Parse(line)!["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]!.AsArray())
            .Select(s => s!)];

    private static string Attr(JsonNode span, string key) =>
        span["attributes"]!.AsArray().SingleOrDefault(a => (string)a!["key"]! == key)?["value"]?["stringValue"]?.ToString() ?? "";

    [Fact]
    public async Task MathAgent_RunTracedToJsonl_WritesTheParentChildSpanTree()
    {
        using var dir = new TempDirectory();
        var fake = new FakeLlmClient().EnqueueText("{\"result\": 30}").EnqueueText("{\"result\": \"(10 + 5) * 2 is 30 because the sum is doubled.\"}");
        NooaTracing.Enable(new JsonlSpanExporter(dir.Path));
        using var session = TraceSession.Scope("quickstart-06");
        var agent = CreateAgent(fake);

        var result = await agent.RunAsync("(10 + 5) * 2");
        NooaTracing.Flush();

        Assert.Equal("(10 + 5) * 2 is 30 because the sum is doubled.", result);
        var spans = ReadSpans(dir.File("quickstart-06.jsonl"));
        var byId = spans.ToDictionary(s => (string)s["spanId"]!);
        string Parent(JsonNode s) => (string?)s["parentSpanId"] is { } id ? (string)byId[id]["name"]! : "";

        var run = Assert.Single(spans, s => (string)s["name"]! == "method.RunAsync");
        var calculate = Assert.Single(spans, s => (string)s["name"]! == "method.CalculateAsync");
        var explain = Assert.Single(spans, s => (string)s["name"]! == "method.ExplainAsync");
        Assert.Equal("", Parent(run));
        Assert.Equal("method.RunAsync", Parent(calculate));
        Assert.Equal("method.RunAsync", Parent(explain));
        Assert.Single(spans.Select(s => (string)s["traceId"]!).Distinct());
        Assert.Equal(3, spans.Count(s => (string)s["name"]! == "generation"));
        Assert.Equal(2, spans.Count(s => (string)s["name"]! == "llm"));
        Assert.All(spans.Where(s => (string)s["name"]! == "llm"), s => Assert.Equal("generation", Parent(s)));
        Assert.All(spans, s => Assert.Equal("quickstart-06", Attr(s, "session.id")));
        Assert.Equal("AGENT", Attr(run, "openinference.span.kind"));
        Assert.Equal("LLM", Attr(spans.First(s => (string)s["name"]! == "llm"), "openinference.span.kind"));
        Assert.Equal(result, Attr(run, "output.value"));
        Assert.Equal(8, spans.Count);
    }

    [Fact]
    public async Task MathAgent_SecretsInPromptsAndEnvironment_NeverReachTheTraceFile()
    {
        using var dir = new TempDirectory();
        using var env = new EnvVars();
        env.Set("QS06_SERVICE_API_KEY", "env-credential-value-1234567890");
        var fake = new FakeLlmClient().EnqueueText("{\"result\": 1}").EnqueueText("{\"result\": \"echo sk-abcdefghijklmnopqrstuv and env-credential-value-1234567890\"}");
        NooaTracing.Enable(new JsonlSpanExporter(dir.Path));
        using var session = TraceSession.Scope("quickstart-06-secrets");

        await CreateAgent(fake).RunAsync("1 + 0 with Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abcdef");
        NooaTracing.Flush();

        var text = File.ReadAllText(dir.File("quickstart-06-secrets.jsonl"));
        Assert.DoesNotContain("eyJhbGci", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuv", text, StringComparison.Ordinal);
        Assert.DoesNotContain("env-credential-value-1234567890", text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MathAgent_TracingOff_RunsWithoutSpans()
    {
        var fake = new FakeLlmClient().EnqueueText("{\"result\": 30}").EnqueueText("{\"result\": \"done\"}");

        Assert.Equal("done", await CreateAgent(fake).RunAsync("(10 + 5) * 2"));
    }
}
