// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Nooa.Llm;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class TracingLlmClientTests
{
    private static string Tag(Activity span, string key) => SpanCapture.Tag(span, key);

    [Fact]
    public async Task CallAsync_RecordsAnLlmSpanWithMessagesAndUsage()
    {
        using var capture = new SpanCapture();
        var fake = new FakeLlmClient().Enqueue(new LlmResponse
        {
            Parts = [new AssistantText { Text = "four" }, new ToolCall { Id = "c1", Name = "add", Arguments = "{\"a\":2}" }],
            ModelName = "test-model",
            Usage = new LlmUsage { InputTokens = 10, OutputTokens = 3, TotalTokens = 13, CachedInputTokens = 4, ReasoningTokens = 1 },
            FinishReason = FinishReason.ToolCalls,
        });
        var client = new TracingLlmClient(fake);

        var response = await client.CallAsync([LlmMessage.System("be brief"), LlmMessage.User("2+2?")], tools: [new ToolDefinition("add", "adds", new System.Text.Json.Nodes.JsonObject())], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("four", response.Content);
        var span = capture.Single("llm");
        Assert.Equal("LLM", Tag(span, "openinference.span.kind"));
        Assert.Equal("test-model", Tag(span, "llm.model_name"));
        Assert.Equal("system", Tag(span, "llm.input_messages.0.message.role"));
        Assert.Equal("be brief", Tag(span, "llm.input_messages.0.message.content"));
        Assert.Equal("2+2?", Tag(span, "llm.input_messages.1.message.content"));
        Assert.Equal("four", Tag(span, "llm.output_messages.0.message.content"));
        Assert.Equal("add", Tag(span, "llm.output_messages.0.message.tool_calls.0.tool_call.function.name"));
        Assert.Equal("{\"a\":2}", Tag(span, "llm.output_messages.0.message.tool_calls.0.tool_call.function.arguments"));
        Assert.Equal("add", Tag(span, "llm.tools.0.tool.name"));
        Assert.Equal("10", Tag(span, "llm.token_count.prompt"));
        Assert.Equal("3", Tag(span, "llm.token_count.completion"));
        Assert.Equal("13", Tag(span, "llm.token_count.total"));
        Assert.Equal("4", Tag(span, "llm.token_count.prompt_details.cache_read"));
        Assert.Equal("1", Tag(span, "llm.token_count.completion_details.reasoning"));
        Assert.Equal("ToolCalls", Tag(span, "llm.finish_reason"));
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
    }

    [Fact]
    public async Task CallAsync_ReplayedAssistantTurnAndToolResult_AreRecorded()
    {
        using var capture = new SpanCapture();
        var earlier = new LlmResponse { Parts = [new AssistantText { Text = "earlier" }, new ToolCall { Id = "c1", Name = "f", Arguments = "{}" }] };
        var client = new TracingLlmClient(FakeLlmClient.SimpleMessage("ok"));

        await client.CallAsync([LlmMessage.User("q"), new ResponseMessage(earlier), LlmMessage.Tool("c1", "tool-output"), new CacheBoundary()], cancellationToken: TestContext.Current.CancellationToken);

        var span = capture.Single("llm");
        Assert.Equal("assistant", Tag(span, "llm.input_messages.1.message.role"));
        Assert.Equal("f", Tag(span, "llm.input_messages.1.message.tool_calls.0.tool_call.function.name"));
        Assert.Equal("tool", Tag(span, "llm.input_messages.2.message.role"));
        Assert.Equal("c1", Tag(span, "llm.input_messages.2.message.tool_call_id"));
        Assert.Equal("", Tag(span, "llm.input_messages.3.message.role"));
    }

    [Fact]
    public async Task CallAsync_SecretInMessage_IsScrubbed()
    {
        using var capture = new SpanCapture();
        var client = new TracingLlmClient(FakeLlmClient.SimpleMessage("your key is sk-abcdefghijklmnopqrstuv"));

        await client.CallAsync([LlmMessage.User("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abcdef")], cancellationToken: TestContext.Current.CancellationToken);

        var span = capture.Single("llm");
        var all = string.Concat(span.TagObjects.Select(t => t.Value?.ToString()));
        Assert.DoesNotContain("eyJhbGci", all, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuv", all, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallAsync_ManyMessages_KeepsTheFirstAndLastHalves()
    {
        using var capture = new SpanCapture();
        var client = new TracingLlmClient(FakeLlmClient.SimpleMessage("ok"));
        var messages = Enumerable.Range(0, 300).Select(i => LlmMessage.User("m" + i)).ToList();

        await client.CallAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        var span = capture.Single("llm");
        Assert.Equal("m0", Tag(span, "llm.input_messages.0.message.content"));
        Assert.Equal("m49", Tag(span, "llm.input_messages.49.message.content"));
        Assert.Equal("m250", Tag(span, "llm.input_messages.50.message.content"));
        Assert.Equal("m299", Tag(span, "llm.input_messages.99.message.content"));
        Assert.Equal("", Tag(span, "llm.input_messages.100.message.content"));
        Assert.Equal("200", Tag(span, "nooa.llm.input_messages.omitted"));
    }

    [Fact]
    public async Task CallAsync_HugeContent_IsBounded()
    {
        using var capture = new SpanCapture();
        var client = new TracingLlmClient(FakeLlmClient.SimpleMessage(new string('o', 200_000)));

        await client.CallAsync([LlmMessage.User(new string('i', 200_000))], cancellationToken: TestContext.Current.CancellationToken);

        var span = capture.Single("llm");
        Assert.True(Tag(span, "llm.input_messages.0.message.content").Length < 21_000);
        Assert.True(Tag(span, "output.value").Length < 21_000);
    }

    [Fact]
    public async Task CallAsync_Failure_RecordsTheErrorAndRethrows()
    {
        using var capture = new SpanCapture();
        var client = new TracingLlmClient(new FakeLlmClient(strictExhaustion: true));

        await Assert.ThrowsAsync<FakeLlmResponseExhaustedException>(() => client.CallAsync([LlmMessage.User("q")], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ActivityStatusCode.Error, capture.Single("llm").Status);
    }

    [Fact]
    public async Task CallAsync_WithoutListener_JustCallsTheInnerClient()
    {
        var client = new TracingLlmClient(FakeLlmClient.SimpleMessage("ok"));

        var response = await client.CallAsync([LlmMessage.User("q")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ok", response.Content);
    }

    [Fact]
    public async Task CallAsync_WhenSuppressed_RecordsNothing()
    {
        using var capture = new SpanCapture();
        var client = new TracingLlmClient(FakeLlmClient.SimpleMessage("ok"));
        TraceSuppression.IsActive = true;
        try
        {
            await client.CallAsync([LlmMessage.User("q")], cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            TraceSuppression.IsActive = false;
        }

        Assert.Empty(capture.Spans);
    }

    [Fact]
    public void ContextWindowAndCountTokens_ComeFromTheInnerClient()
    {
        var client = new TracingLlmClient(new FakeLlmClient());

        Assert.Equal(128_000, client.ContextWindow);
        Assert.True(client.CountTokens("hello world") > 0);
    }
}
