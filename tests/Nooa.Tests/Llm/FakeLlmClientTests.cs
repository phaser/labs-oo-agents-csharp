// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_llm.py

using System.Text.Json.Nodes;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class FakeLlmClientTests
{
    private sealed record Answer(int Value, string Label);

    private static readonly IReadOnlyList<LlmMessage> _hello = [LlmMessage.User("test")];

    private static LlmResponse Reply(string content) =>
        new() { Parts = [new AssistantText { Text = content }] };

    [Fact]
    public async Task SimpleMessage_Call_ReturnsText()
    {
        var client = FakeLlmClient.SimpleMessage("Hello world");

        var response = await Send(client, _hello);

        Assert.Equal("Hello world", response.Content);
        Assert.Null(response.Reasoning);
        Assert.Empty(response.ToolCalls);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(2, response.Usage!.OutputTokens);
        Assert.Equal(12, response.Usage.TotalTokens);
    }

    [Fact]
    public async Task WithToolCall_Call_ReturnsToolCall()
    {
        var client = FakeLlmClient.WithToolCall("search", new { query = "test" });

        var response = await Send(client, _hello);

        var call = Assert.Single(response.ToolCalls);
        Assert.Equal("search", call.Name);
        Assert.Equal("""{"query":"test"}""", call.Arguments);
        Assert.Equal(FinishReason.ToolCalls, response.FinishReason);
        Assert.Equal(15, response.Usage!.TotalTokens);
        Assert.Single(response.ReplayToolCalls);
    }

    [Fact]
    public async Task WithReasoning_Call_ReturnsReasoningAndText()
    {
        var client = FakeLlmClient.WithReasoning("Let me think about this...", "Here is my answer");

        var response = await Send(client, _hello);

        Assert.Equal("Let me think about this...", response.Reasoning);
        Assert.Equal("Here is my answer", response.Content);
        Assert.Equal(25, response.Usage!.TotalTokens);
    }

    [Fact]
    public async Task WithCodeResponses_Calls_ReturnInOrder()
    {
        var client = FakeLlmClient.WithCodeResponses(["a b", "c"]);

        Assert.Equal("a b", (await Send(client, [])).Content);
        Assert.Equal("c", (await Send(client, [])).Content);
    }

    [Fact]
    public async Task Call_Recorded_CapturesLastMessagesAndTools()
    {
        var client = FakeLlmClient.SimpleMessage("test");
        var tools = new[] { new ToolDefinition("test_tool", "d", new JsonObject()) };

        await Send(client, _hello, tools);

        Assert.Equal(1, client.CallCount);
        Assert.Equal(JsonMessage(_hello[0]).ToJsonString(), JsonMessage(client.LastMessages[0]).ToJsonString());
        Assert.Equal("test_tool", Assert.Single(client.LastTools!).Name);
    }

    [Fact]
    public async Task Reset_AfterCall_ClearsHistoryOnly()
    {
        var client = new FakeLlmClient().EnqueueText("first").EnqueueText("second");
        await Send(client, _hello);

        client.Reset();

        Assert.Equal(0, client.CallCount);
        Assert.Empty(client.Calls);
        Assert.Empty(client.LastMessages);
        Assert.Null(client.LastTools);
        Assert.Equal(1, client.RemainingResponses);
        Assert.Equal("second", (await Send(client, [])).Content);
        Assert.Equal(1, client.Calls[0].Index);
    }

    [Fact]
    public async Task Calls_PastScript_ReturnEmptyResponse()
    {
        var client = new FakeLlmClient([Reply("First response"), Reply("Second response")]);

        Assert.Equal("First response", (await Send(client, [])).Content);
        Assert.Equal("Second response", (await Send(client, [])).Content);
        var third = await Send(client, []);
        Assert.Equal("", third.Content);
        Assert.Equal(FinishReason.Stop, third.FinishReason);
        Assert.All(client.Calls, c => Assert.Null(c.Error));
    }

    [Fact]
    public async Task MultipleResponses_Calls_KeepOrderAndToolCalls()
    {
        var withTool = new LlmResponse
        {
            Parts = [new AssistantText { Text = "Response 3" }, new ToolCall { Id = "1", Name = "test_tool", Arguments = "{}" }],
            FinishReason = FinishReason.ToolCalls,
        };
        var client = new FakeLlmClient([Reply("Response 1"), Reply("Response 2"), withTool]);

        await Send(client, []);
        await Send(client, []);
        var r3 = await Send(client, []);

        Assert.Equal("Response 3", r3.Content);
        Assert.Equal("test_tool", Assert.Single(r3.ToolCalls).Name);
    }

    [Fact]
    public async Task Constructor_RepeatedResponse_BecomesDistinctEvents()
    {
        var shared = Reply("same");
        var client = new FakeLlmClient([shared, shared, shared]);

        var first = await Send(client, []);
        var second = await Send(client, []);
        var third = await Send(client, []);

        Assert.Same(shared, first);
        Assert.NotSame(shared, second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, third.Id);
        Assert.Equal("same", third.Content);
    }

    [Fact]
    public async Task EnqueueStructured_Call_ReturnsJsonAndParsedValue()
    {
        var client = new FakeLlmClient().EnqueueStructured(new Answer(42, "x"));

        var response = await Send(client, _hello, outputType: typeof(Answer));

        Assert.Equal(new Answer(42, "x"), response.Parsed);
        Assert.Equal("""{"value":42,"label":"x"}""", response.Content);
        Assert.Equal(typeof(Answer), client.Calls[0].OutputType);
    }

    [Fact]
    public async Task Enqueue_Chained_KeepsOrder()
    {
        var client = new FakeLlmClient()
            .EnqueueToolCall("lookup", message: "calling")
            .EnqueueText("done");

        var first = await Send(client, []);
        var second = await Send(client, []);

        Assert.Equal("calling", first.Content);
        Assert.Equal("{}", Assert.Single(first.ToolCalls).Arguments);
        Assert.Equal("done", second.Content);
    }

    [Fact]
    public async Task Transcript_AfterCall_IsDetachedFromInputs()
    {
        var client = new FakeLlmClient([Reply("done")]);
        var message = new JsonObject { ["role"] = "user", ["content"] = "before" };
        var schema = new JsonObject { ["type"] = "object" };

        var returned = await Send(client,
            [new JsonMessage(message)], [new ToolDefinition("lookup", "Look up", schema)], typeof(Answer));
        message["content"] = "after";
        schema["type"] = "changed";

        Assert.Equal(0, client.RemainingResponses);
        var call = Assert.Single(client.Calls);
        Assert.Equal(1, call.Index);
        Assert.Equal("before", ((JsonMessage)call.Messages[0]).Value["content"]!.GetValue<string>());
        Assert.Equal("object", Assert.Single(call.Tools!).ParametersSchema["type"]!.GetValue<string>());
        Assert.Equal(typeof(Answer), call.OutputType);
        Assert.NotSame(returned, call.Response);
        Assert.Equal("done", call.Response!.Content);
        Assert.Null(call.Error);
    }

    [Fact]
    public async Task StrictExhaustion_Async_RecordsFailure()
    {
        var client = new FakeLlmClient([Reply("only")], strictExhaustion: true);

        Assert.Equal("only", (await Send(client, [LlmMessage.User("first")])).Content);
        var error = await Assert.ThrowsAsync<FakeLlmResponseExhaustedException>(
            () => Send(client, [LlmMessage.User("unexpected")]));

        Assert.Contains("call 2", error.Message);
        Assert.Equal(2, client.CallCount);
        Assert.Equal(0, client.RemainingResponses);
        var failed = client.Calls[1];
        Assert.Equal(2, failed.Index);
        Assert.Null(failed.Response);
        Assert.Same(error, failed.Error);
    }

    [Fact]
    public async Task SimpleMessage_StrictMode_ThrowsAfterMessage()
    {
        var client = FakeLlmClient.SimpleMessage("only", strictExhaustion: true);

        Assert.Equal("only", (await Send(client, [])).Content);
        await Assert.ThrowsAsync<FakeLlmResponseExhaustedException>(() => Send(client, []));
    }

    [Fact]
    public async Task ConcurrentCalls_Succeed_InScriptOrder()
    {
        const int count = 50;
        var client = new FakeLlmClient(Enumerable.Range(0, count).Select(i => Reply($"response-{i}")));

        var results = await Task.WhenAll(Enumerable.Range(0, count).Select(i => Send(client, [LlmMessage.User($"request-{i}")])));

        Assert.Equal(Enumerable.Range(0, count).Select(i => $"response-{i}"), results.Select(r => r.Content));
        Assert.Equal(Enumerable.Range(1, count), client.Calls.Select(c => c.Index));
        Assert.Equal(0, client.RemainingResponses);
    }

    [Fact]
    public async Task ConcurrentStrictCalls_RecordEveryAttempt()
    {
        var client = new FakeLlmClient(Enumerable.Range(0, 10).Select(i => Reply($"r{i}")), strictExhaustion: true);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 50).Select(async i =>
        {
            try
            {
                return await Send(client, [LlmMessage.User($"q{i}")]);
            }
            catch (FakeLlmResponseExhaustedException)
            {
                return null;
            }
        }));

        Assert.Equal(10, outcomes.Count(o => o is not null));
        Assert.Equal(50, client.Calls.Count);
        Assert.Equal(Enumerable.Range(1, 50), client.Calls.Select(c => c.Index));
        Assert.Equal(10, client.Calls.Count(c => c.Response is not null));
        Assert.Equal(40, client.Calls.Count(c => c.Error is not null));
    }

    [Fact]
    public async Task CallAsync_CancelledToken_Throws()
    {
        var client = FakeLlmClient.SimpleMessage("x");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CallAsync([], cancellationToken: cts.Token));

        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public void CountTokens_Text_IsCharsOverFourPlusOne()
    {
        var client = new FakeLlmClient();

        Assert.Equal(1, client.CountTokens(""));
        Assert.Equal(3, client.CountTokens(new string('a', 8)));
        Assert.Equal(128_000, client.ContextWindow);
    }

    private static Task<LlmResponse> Send(
        FakeLlmClient client,
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null) =>
        client.CallAsync(messages, tools, outputType, TestContext.Current.CancellationToken);

    private static JsonNode JsonMessage(LlmMessage message) => ((JsonMessage)message).Value;
}
