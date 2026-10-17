// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_tracing_integration.py, test_concurrent_spans.py and test_context_isolation.py

#pragma warning disable xUnit1051 // The agentic methods take an optional token that these tests do not need.

using System.Diagnostics;
using System.Text.Json.Nodes;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Tests.Runtime;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class OpenInferenceHooksTests
{
    private static AgentOptions Options(ILlmClient llm) => new() { Llm = llm, Strategies = [] };

    private static string Tag(Activity span, string key) => SpanCapture.Tag(span, key);

    [Fact]
    public async Task AgentCall_EmitsAnAgentSpanWithOpenInferenceAttributes()
    {
        using var capture = new SpanCapture();
        using var session = TraceSession.Scope("s-1");
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("answer")));

        var result = await agent.AskAsync("question");

        Assert.Equal("answer", result);
        var span = capture.Single("method.AskAsync");
        Assert.Equal("AGENT", Tag(span, "openinference.span.kind"));
        Assert.Equal("method", Tag(span, "nooa.viewer.plugin"));
        Assert.Equal("EchoAgent", Tag(span, "agent.name"));
        Assert.Equal("s-1", Tag(span, "session.id"));
        Assert.StartsWith("AskAsync(", Tag(span, "agent.method_signature"), StringComparison.Ordinal);
        Assert.Contains("String question", Tag(span, "agent.method_signature"), StringComparison.Ordinal);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal("answer", Tag(span, "output.value"));
        Assert.Equal("text/plain", Tag(span, "output.mime_type"));
        var input = JsonNode.Parse(Tag(span, "input.value"))!;
        Assert.Equal("question", (string)input["kwargs"]!["question"]!);
        Assert.Equal("application/json", Tag(span, "input.mime_type"));
        Assert.Contains(span.Events, e => e.Name == "status_update");
    }

    [Fact]
    public async Task Generation_EmitsAChainSpanUnderTheAgentSpan()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("answer")));

        await agent.AskAsync("question");

        var call = capture.Single("method.AskAsync");
        var generation = capture.Single("generation");
        Assert.Equal("CHAIN", Tag(generation, "openinference.span.kind"));
        Assert.Equal("EchoStrategy", Tag(generation, "generation.strategy"));
        Assert.Equal(call.SpanId, generation.ParentSpanId);
        Assert.Equal(call.TraceId, generation.TraceId);
        Assert.Equal("String", Tag(generation, "result.type"));
        Assert.Null(call.GetTagItem("result.type"));
    }

    [Fact]
    public async Task NestedCall_IsAChildOfTheOuterGeneration()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("inner-answer")));

        await agent.OuterAsync("q");

        var outer = capture.Single("method.OuterAsync");
        var inner = capture.Single("method.AskAsync");
        Assert.Equal(outer.TraceId, inner.TraceId);
        Assert.Equal(outer.SpanId, inner.ParentSpanId);
        Assert.Equal(Tag(outer, "agent.call_id"), Tag(inner, "agent.parent_call_id"));
        Assert.Equal(4, capture.Spans.Count);
    }

    [Fact]
    public async Task SequentialCalls_AreSeparateTraces()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new FakeLlmClient().EnqueueText("a").EnqueueText("b")));

        await agent.AskAsync("1");
        await agent.AskAsync("2");

        var calls = capture.Spans.Where(s => s.DisplayName == "method.AskAsync").ToList();
        Assert.Equal(2, calls.Count);
        Assert.NotEqual(calls[0].TraceId, calls[1].TraceId);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task FailedCall_RecordsTheErrorOnBothSpans()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new FakeLlmClient()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.FailAsync("q"));

        foreach (var name in new[] { "method.FailAsync", "generation" })
        {
            var span = capture.Single(name);
            Assert.Equal(ActivityStatusCode.Error, span.Status);
            Assert.Equal("InvalidOperationException", Tag(span, "error.type"));
            Assert.Equal("strategy failed", Tag(span, "error.message"));
        }
    }

    [Fact]
    public async Task NoTraceMethod_EmitsNoAgentSpanAndNoGenerationSpan()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("secret-result")));

        await agent.QuietAsync("secret-argument");

        Assert.Empty(capture.Spans);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task NoTraceMethod_SuppressesLlmSpansInsideItsGeneration()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new TracingLlmClient(FakeLlmClient.SimpleMessage("secret-result"))));

        await agent.QuietAsync("secret-argument");
        await agent.AskAsync("visible");

        Assert.DoesNotContain(capture.Spans, s => Tag(s, "llm.input_messages.0.message.content") == "secret-argument");
        Assert.False(TraceSuppression.IsActive);
    }

    [Fact]
    public async Task NoTraceMethod_DoesNotLeakItsArgumentsIntoAnyTag()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new TracingLlmClient(new FakeLlmClient().EnqueueText("secret-result").EnqueueText("other"))));

        await agent.QuietAsync("secret-argument");
        await agent.AskAsync("ordinary");

        var all = capture.Spans.SelectMany(s => s.TagObjects).Select(t => t.Value?.ToString());
        Assert.DoesNotContain(all, v => v is not null && (v.Contains("secret-argument", StringComparison.Ordinal) || v.Contains("secret-result", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task NestedCall_TracesTheLlmCallOfTheInnerMethod()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new TracingLlmClient(FakeLlmClient.SimpleMessage("x"))));

        await agent.OuterAsync("q");

        Assert.Single(capture.Spans, s => s.DisplayName == "llm");
    }

    [Fact]
    public async Task ArgumentWithSecret_IsScrubbedInInputValue()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

        await agent.AskAsync("use OPENAI_API_KEY=sk-abcdefghijklmnopqrstuv please");

        var input = Tag(capture.Single("method.AskAsync"), "input.value");
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuv", input, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", input, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HugeArgument_IsBounded()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

        await agent.AskAsync(new string('x', 500_000));

        Assert.True(Tag(capture.Single("method.AskAsync"), "input.value").Length <= SpanAttributes.MaxValueChars);
    }

    [Fact]
    public async Task ConcurrentCalls_KeepSeparateTraces()
    {
        using var capture = new SpanCapture();
        var agents = Enumerable.Range(0, 8).Select(i => new EchoAgent(Options(FakeLlmClient.SimpleMessage("r" + i)))).ToList();

        await Task.WhenAll(agents.Select((a, i) => Task.Run(() => a.AskAsync("q" + i))));

        var calls = capture.Spans.Where(s => s.DisplayName == "method.AskAsync").ToList();
        Assert.Equal(8, calls.Count);
        Assert.Equal(8, calls.Select(s => s.TraceId).Distinct().Count());
        Assert.Equal(16, capture.Spans.Select(s => s.SpanId).Distinct().Count());
        foreach (var call in calls)
        {
            Assert.Single(capture.Spans, s => s.DisplayName == "generation" && s.ParentSpanId == call.SpanId);
        }
    }

    [Fact]
    public void EndActiveSpans_EndsOpenSpansWithAnError()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new FakeLlmClient()));
        var context = capture.Hooks.BeforeAgentCall(agent, "AskAsync", [], "call-1", null);
        Assert.NotNull(context);

        var ended = capture.Hooks.EndActiveSpans("task timeout");

        Assert.Equal(1, ended);
        var span = capture.Single("method.AskAsync");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("Timeout", Tag(span, "error.type"));
        Assert.Equal(0, capture.Hooks.EndActiveSpans());
    }

    [Fact]
    public void AfterAgentCall_WithUnknownContext_DoesNothing()
    {
        using var capture = new SpanCapture();
        var agent = new EchoAgent(Options(new FakeLlmClient()));

        capture.Hooks.AfterAgentCall(null, agent, "AskAsync", null, null);
        capture.Hooks.AfterGeneration("not a scope", agent, "AskAsync", null, null, "g");

        Assert.Empty(capture.Spans);
    }

    [Fact]
    public async Task WithoutAListener_NoSpanIsCreatedAndTheCallWorks()
    {
        var hooks = new OpenInferenceHooks();
        InstrumentationHooks.Current = hooks;
        try
        {
            var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

            Assert.Equal("ok", await agent.AskAsync("q"));
        }
        finally
        {
            InstrumentationHooks.Current = null;
        }
    }
}
