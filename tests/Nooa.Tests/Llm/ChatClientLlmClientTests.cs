// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_empty_content_retry.py, tests/unifiedllm/test_finish_reason_propagation.py, tests/unifiedllm/test_output_model_instantiation.py, tests/unifiedllm/test_provider_call_deadline.py

using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ChatClientLlmClientTests
{
    private sealed record Sentiment(string Label, double Score);

    private static readonly IReadOnlyList<LlmMessage> _hello = [LlmMessage.User("hi")];

    private static RetryPolicy FastRetry(RetryOptions? options = null) =>
        new(options ?? new RetryOptions(), (_, _) => Task.CompletedTask, () => 0.0);

    private static ChatClientLlmClient Client(ScriptedChatClient chat, ChatClientLlmOptions? options = null, RetryPolicy? retry = null) =>
        new(chat, options, retry ?? FastRetry(options?.Retry));

    private static Task<LlmResponse> Call(
        ChatClientLlmClient client,
        IReadOnlyList<LlmMessage>? messages = null,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null) =>
        client.CallAsync(messages ?? _hello, tools, outputType, TestContext.Current.CancellationToken);

    private static ChatResponse Response(params AIContent[] contents) =>
        new(new ChatMessage(ChatRole.Assistant, contents));

    [Fact]
    public async Task CallAsync_Text_ReturnsAnAssistantTextPart()
    {
        var chat = new ScriptedChatClient().Reply("Hello");

        var response = await Call(Client(chat));

        Assert.Equal("Hello", response.Content);
        Assert.Equal(FinishReason.Stop, response.FinishReason);
        Assert.Empty(response.ToolCalls);
        Assert.IsType<ChatResponse>(response.RawResponse);
    }

    [Fact]
    public async Task CallAsync_Messages_AreMappedToChatMessages()
    {
        var chat = new ScriptedChatClient().Reply("ok");
        var messages = new List<LlmMessage>
        {
            LlmMessage.System("be brief"),
            LlmMessage.User("hi"),
            new JsonMessage(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = "",
                ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = "c1",
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = "search", ["arguments"] = "{\"q\":\"x\"}" },
                }),
            }),
            LlmMessage.Tool("c1", "result"),
        };

        await Call(Client(chat), messages);

        var sent = chat.Calls.Single().Messages;
        Assert.Equal([ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.Tool], sent.Select(m => m.Role));
        Assert.Equal("be brief", sent[0].Text);
        var call = Assert.IsType<FunctionCallContent>(Assert.Single(sent[2].Contents));
        Assert.Equal(("c1", "search"), (call.CallId, call.Name));
        Assert.Equal("x", call.Arguments!["q"]!.ToString());
        var result = Assert.IsType<FunctionResultContent>(Assert.Single(sent[3].Contents));
        Assert.Equal(("c1", "result"), (result.CallId, result.Result));
    }

    [Fact]
    public async Task CallAsync_ResponseMessage_ReplaysTextAndOnlyReplayableToolCalls()
    {
        var chat = new ScriptedChatClient().Reply("ok");
        var earlier = new LlmResponse
        {
            Parts =
            [
                new AssistantReasoning { Text = "private" },
                new AssistantText { Text = "calling" },
                new ToolCall { Id = "c1", Name = "search", Arguments = "{\"q\":1}" },
            ],
            FinishReason = FinishReason.ToolCalls,
        };

        await Call(Client(chat), [LlmMessage.User("hi"), new ResponseMessage(earlier), LlmMessage.Tool("c1", "r")]);

        var replay = chat.Calls.Single().Messages[1];
        Assert.Equal(ChatRole.Assistant, replay.Role);
        Assert.Equal("calling", replay.Text);
        Assert.Single(replay.Contents.OfType<FunctionCallContent>());
        Assert.DoesNotContain("private", replay.Contents.OfType<TextContent>().Select(t => t.Text));
    }

    [Fact]
    public async Task CallAsync_MalformedToolCallInHistory_IsLeftOutOfReplay()
    {
        var chat = new ScriptedChatClient().Reply("ok");
        var earlier = new LlmResponse
        {
            Parts = [new ToolCall { Id = "c1", Name = "search", Arguments = "{not json" }],
            FinishReason = FinishReason.ToolCalls,
        };

        await Call(Client(chat), [new ResponseMessage(earlier)]);

        Assert.Empty(chat.Calls.Single().Messages[0].Contents.OfType<FunctionCallContent>());
    }

    [Fact]
    public async Task CallAsync_MalformedToolCallInJsonMessage_Throws()
    {
        var message = new JsonMessage(new JsonObject
        {
            ["role"] = "assistant",
            ["tool_calls"] = new JsonArray(new JsonObject
            {
                ["id"] = "c1",
                ["function"] = new JsonObject { ["name"] = "t", ["arguments"] = "{not json" },
            }),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => Call(Client(new ScriptedChatClient()), [message]));
    }

    [Fact]
    public async Task CallAsync_OneCacheBoundary_IsConsumed()
    {
        var chat = new ScriptedChatClient().Reply("ok");

        await Call(Client(chat), [LlmMessage.System("stable"), new CacheBoundary(), LlmMessage.User("dynamic")]);

        Assert.Equal(2, chat.Calls.Single().Messages.Count);
    }

    [Fact]
    public async Task CallAsync_TwoCacheBoundaries_Throw()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Call(Client(new ScriptedChatClient()), [new CacheBoundary(), LlmMessage.User("x"), new CacheBoundary()]));
    }

    [Fact]
    public async Task CallAsync_ReasoningTextAndToolCall_KeepOrderAndSetToolCallsFinish()
    {
        var chat = new ScriptedChatClient().Reply(Response(
            new TextReasoningContent("thinking"),
            new TextContent("calling"),
            new FunctionCallContent("c1", "search", new Dictionary<string, object?> { ["q"] = "x" })));

        var response = await Call(Client(chat));

        Assert.Equal(["AssistantReasoning", "AssistantText", "ToolCall"], response.Parts.Select(p => p.GetType().Name));
        Assert.Equal("thinking", response.Reasoning);
        var call = Assert.Single(response.ToolCalls);
        Assert.Equal(("c1", "search", "{\"q\":\"x\"}"), (call.Id, call.Name, call.Arguments));
        Assert.Equal(FinishReason.ToolCalls, response.FinishReason);
    }

    [Theory]
    [InlineData("stop", FinishReason.Stop)]
    [InlineData("length", FinishReason.Length)]
    [InlineData("tool_calls", FinishReason.ToolCalls)]
    [InlineData("content_filter", FinishReason.Error)]
    [InlineData(null, FinishReason.Stop)]
    public async Task CallAsync_FinishReason_IsNormalized(string? reason, FinishReason expected)
    {
        var raw = Response(new TextContent("x"));
        raw.FinishReason = reason is null ? null : new ChatFinishReason(reason);
        var chat = new ScriptedChatClient().Reply(raw);

        Assert.Equal(expected, (await Call(Client(chat))).FinishReason);
    }

    [Fact]
    public async Task CallAsync_LengthWithToolCalls_KeepsLength()
    {
        var raw = Response(new FunctionCallContent("c1", "t", new Dictionary<string, object?>()));
        raw.FinishReason = ChatFinishReason.Length;

        var response = await Call(Client(new ScriptedChatClient().Reply(raw)));

        Assert.Equal(FinishReason.Length, response.FinishReason);
    }

    [Fact]
    public async Task CallAsync_Usage_IsNormalized()
    {
        var raw = Response(new TextContent("x"));
        raw.Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 4, CachedInputTokenCount = 6, ReasoningTokenCount = 3 };
        raw.ModelId = "model-1";

        var response = await Call(Client(new ScriptedChatClient().Reply(raw)));

        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(4, response.Usage.OutputTokens);
        Assert.Equal(6, response.Usage.CachedInputTokens);
        Assert.Equal(3, response.Usage.ReasoningTokens);
        Assert.Equal(14, response.Usage.TotalTokens);
        Assert.Equal("model-1", response.ModelName);
    }

    [Fact]
    public async Task CallAsync_NoUsage_LeavesUsageNull()
    {
        Assert.Null((await Call(Client(new ScriptedChatClient().Reply("x")))).Usage);
    }

    [Fact]
    public async Task CallAsync_Options_CarryModelAndTokenBudget()
    {
        var chat = new ScriptedChatClient().Reply("x");

        await Call(Client(chat, new ChatClientLlmOptions { Model = "m1", MaxOutputTokens = 123 }));

        var options = chat.Calls.Single().Options!;
        Assert.Equal("m1", options.ModelId);
        Assert.Equal(123, options.MaxOutputTokens);
    }

    [Fact]
    public async Task CallAsync_ThinkTagsDisabled_LeavesTextAlone()
    {
        var response = await Call(Client(new ScriptedChatClient().Reply("<think>a</think>b")));

        Assert.Equal("<think>a</think>b", response.Content);
        Assert.Null(response.Reasoning);
    }

    [Fact]
    public async Task CallAsync_ThinkTagsEnabled_MovesThoughtsToReasoning()
    {
        var chat = new ScriptedChatClient().Reply("<think>let me see</think>The answer.");

        var response = await Call(Client(chat, new ChatClientLlmOptions { ExtractThinkTags = true }));

        Assert.Equal("The answer.", response.Content);
        Assert.Equal("let me see", response.Reasoning);
    }

    [Fact]
    public async Task CallAsync_Tools_BecomeDeclarationsWithStrictSchemas()
    {
        var chat = new ScriptedChatClient().Reply("x");
        var tool = new ToolDefinition("search", "Search the web", (JsonObject)JsonNode.Parse(
            """{"type":"object","title":"X","properties":{"q":{"type":"string","title":"Q"}},"required":["q"]}""")!);

        await Call(Client(chat), tools: [tool]);

        var declaration = Assert.IsAssignableFrom<AIFunctionDeclaration>(Assert.Single(chat.Calls.Single().Options!.Tools!));
        Assert.Equal(("search", "Search the web"), (declaration.Name, declaration.Description));
        Assert.Equal(true, declaration.AdditionalProperties["strict"]);
        var schema = JsonNode.Parse(declaration.JsonSchema.GetRawText())!;
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        Assert.DoesNotContain("title", schema.ToJsonString());
    }

    [Fact]
    public async Task CallAsync_ToolWithOptionalParameter_FallsBackToLooseSchema()
    {
        var chat = new ScriptedChatClient().Reply("x");
        var tool = new ToolDefinition("t", "d", (JsonObject)JsonNode.Parse(
            """{"type":"object","properties":{"a":{"type":"string"},"b":{"type":"integer"}},"required":["a"]}""")!);

        await Call(Client(chat), tools: [tool]);

        var declaration = (AIFunctionDeclaration)chat.Calls.Single().Options!.Tools![0];
        Assert.Equal(false, declaration.AdditionalProperties["strict"]);
        Assert.DoesNotContain("additionalProperties", declaration.JsonSchema.GetRawText());
    }

    [Fact]
    public async Task CallAsync_ToolWithUntypedProperty_FallsBackToLooseSchema()
    {
        var chat = new ScriptedChatClient().Reply("x");
        var tool = new ToolDefinition("t", "d", (JsonObject)JsonNode.Parse(
            """{"type":"object","properties":{"a":{}},"required":["a"]}""")!);

        await Call(Client(chat), tools: [tool]);

        Assert.Equal(false, ((AIFunctionDeclaration)chat.Calls.Single().Options!.Tools![0]).AdditionalProperties["strict"]);
    }

    [Fact]
    public async Task CallAsync_StrictToolsOff_AlwaysSendsLooseSchema()
    {
        var chat = new ScriptedChatClient().Reply("x");
        var tool = new ToolDefinition("t", "d", (JsonObject)JsonNode.Parse(
            """{"type":"object","properties":{"a":{"type":"string"}},"required":["a"]}""")!);

        await Call(Client(chat, new ChatClientLlmOptions { StrictTools = false }), tools: [tool]);

        Assert.Equal(false, ((AIFunctionDeclaration)chat.Calls.Single().Options!.Tools![0]).AdditionalProperties["strict"]);
    }

    [Fact]
    public async Task CallAsync_NoTools_SendsNoToolList()
    {
        var chat = new ScriptedChatClient().Reply("x");

        await Call(Client(chat));

        Assert.Null(chat.Calls.Single().Options!.Tools);
    }

    [Fact]
    public async Task CallAsync_StructuredOutput_RequestsSchemaAndParsesTheValue()
    {
        var chat = new ScriptedChatClient().Reply("{\"label\":\"positive\",\"score\":0.9}");

        var response = await Call(Client(chat), outputType: typeof(Sentiment));

        Assert.Equal(new Sentiment("positive", 0.9), response.Parsed);
        var options = chat.Calls.Single().Options!;
        var format = Assert.IsType<ChatResponseFormatJson>(options.ResponseFormat);
        Assert.Equal("Sentiment", format.SchemaName);
        Assert.Equal(true, options.AdditionalProperties!["strict"]);
        Assert.Contains("\"additionalProperties\":false", format.Schema!.Value.GetRawText());
    }

    [Fact]
    public async Task CallAsync_StructuredOutputInFence_IsParsed()
    {
        var chat = new ScriptedChatClient().Reply("```json\n{\"label\":\"x\",\"score\":1}\n```");

        var response = await Call(Client(chat), outputType: typeof(Sentiment));

        Assert.Equal(new Sentiment("x", 1), response.Parsed);
    }

    [Fact]
    public async Task CallAsync_ScalarOutput_IsWrappedForTheRequestAndUnwrapped()
    {
        var chat = new ScriptedChatClient().Reply("{\"result\":42}");

        var response = await Call(Client(chat), outputType: typeof(int));

        Assert.Equal(42, response.Parsed);
        var format = (ChatResponseFormatJson)chat.Calls.Single().Options!.ResponseFormat!;
        var schema = JsonNode.Parse(format.Schema!.Value.GetRawText())!;
        Assert.Equal("object", schema["type"]!.GetValue<string>());
        Assert.Equal("integer", schema["properties"]!["result"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task CallAsync_ListOutput_IsWrappedAndUnwrapped()
    {
        var chat = new ScriptedChatClient().Reply("{\"result\":[{\"label\":\"a\",\"score\":1}]}");

        var response = await Call(Client(chat), outputType: typeof(List<Sentiment>));

        Assert.Equal([new Sentiment("a", 1)], Assert.IsType<List<Sentiment>>(response.Parsed));
    }

    [Fact]
    public async Task CallAsync_FreeFormDictionaryOutput_UsesNonStrictFormat()
    {
        var chat = new ScriptedChatClient().Reply("{\"a\":1}");

        var response = await Call(Client(chat), outputType: typeof(Dictionary<string, int>));

        Assert.Equal(1, Assert.IsType<Dictionary<string, int>>(response.Parsed)["a"]);
        Assert.Equal(false, chat.Calls.Single().Options!.AdditionalProperties!["strict"]);
    }

    [Fact]
    public async Task CallAsync_StructuredOutputWithEmptyContent_FallsBackToReasoning()
    {
        var chat = new ScriptedChatClient().Reply(Response(new TextReasoningContent("{\"label\":\"r\",\"score\":2}")));

        var response = await Call(Client(chat), outputType: typeof(Sentiment));

        Assert.Equal(new Sentiment("r", 2), response.Parsed);
    }

    [Fact]
    public async Task CallAsync_StructuredOutputPrefersContentOverReasoning()
    {
        var chat = new ScriptedChatClient().Reply(Response(
            new TextReasoningContent("{\"label\":\"reasoning\",\"score\":0}"),
            new TextContent("{\"label\":\"content\",\"score\":1}")));

        var response = await Call(Client(chat), outputType: typeof(Sentiment));

        Assert.Equal("content", ((Sentiment)response.Parsed!).Label);
    }

    [Fact]
    public async Task CallAsync_StructuredOutputBothEmpty_ThrowsTypedError()
    {
        var chat = new ScriptedChatClient().Reply(Response());

        var error = await Assert.ThrowsAsync<StructuredOutputException>(() => Call(Client(chat), outputType: typeof(Sentiment)));

        Assert.Equal(typeof(Sentiment), error.OutputType);
        Assert.Equal("", error.Content);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"label\": 5, \"score\": \"x\"}")]
    [InlineData("null")]
    public async Task CallAsync_StructuredOutputInvalid_ThrowsTypedErrorWithTheContent(string text)
    {
        var chat = new ScriptedChatClient().Reply(text);

        var error = await Assert.ThrowsAsync<StructuredOutputException>(() => Call(Client(chat), outputType: typeof(Sentiment)));

        Assert.Equal(text, error.Content);
        Assert.Contains("Sentiment", error.Message);
        Assert.DoesNotContain("not json at all", error.Message);
        Assert.DoesNotContain("\"x\"", error.Message);
    }

    [Theory]
    [InlineData("not json at all", true)]
    [InlineData("{\"label\": \"ok\", \"score\": \"x\"}", false)]
    public async Task CallAsync_StructuredOutputInvalid_FlagsMalformedJsonOnly(string text, bool malformed)
    {
        var chat = new ScriptedChatClient().Reply(text);

        var error = await Assert.ThrowsAsync<StructuredOutputException>(() => Call(Client(chat), outputType: typeof(Sentiment)));

        Assert.Equal(malformed, error.IsMalformedJson);
    }

    [Fact]
    public async Task CallAsync_StructuredOutputTypeMismatch_ReportsThePathNotTheValue()
    {
        var chat = new ScriptedChatClient().Reply("{\"label\": \"ok\", \"score\": \"secret-value\"}");

        var error = await Assert.ThrowsAsync<StructuredOutputException>(() => Call(Client(chat), outputType: typeof(Sentiment)));

        Assert.Equal("$.score", error.Path);
        Assert.DoesNotContain("secret-value", error.Message);
        Assert.DoesNotContain("secret-value", error.Detail);
    }

    [Fact]
    public async Task CallAsync_ScalarOutputWithoutWrapper_ThrowsTypedError()
    {
        var chat = new ScriptedChatClient().Reply("42");

        await Assert.ThrowsAsync<StructuredOutputException>(() => Call(Client(chat), outputType: typeof(int)));
    }

    [Fact]
    public async Task CallAsync_OutputTypeWithToolCalls_SkipsParsing()
    {
        var chat = new ScriptedChatClient().Reply(Response(new FunctionCallContent("c1", "t", new Dictionary<string, object?>())));

        var response = await Call(Client(chat), outputType: typeof(Sentiment));

        Assert.Null(response.Parsed);
        Assert.Single(response.ToolCalls);
    }

    [Fact]
    public async Task CallAsync_TransientErrorThenSuccess_Retries()
    {
        var chat = new ScriptedChatClient()
            .Fail(new HttpRequestException("boom", null, HttpStatusCode.BadGateway))
            .Reply("recovered");

        var response = await Call(Client(chat));

        Assert.Equal("recovered", response.Content);
        Assert.Equal(2, chat.Calls.Count);
    }

    [Fact]
    public async Task CallAsync_PermanentError_IsNotRetried()
    {
        var chat = new ScriptedChatClient().Fail(new HttpRequestException("no", null, HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => Call(Client(chat)));

        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_RetryNull_MakesOneAttempt()
    {
        var chat = new ScriptedChatClient().Fail(new HttpRequestException("boom", null, HttpStatusCode.BadGateway));
        var client = new ChatClientLlmClient(chat, new ChatClientLlmOptions { Retry = null });

        await Assert.ThrowsAsync<HttpRequestException>(() => Call(client));

        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_ZeroRetries_DisablesRetry()
    {
        var chat = new ScriptedChatClient().Fail(new HttpRequestException("boom", null, HttpStatusCode.BadGateway));
        var options = new ChatClientLlmOptions { Retry = new RetryOptions { MaxRetries = 0, RateLimitExtraRetries = 0 } };

        await Assert.ThrowsAsync<HttpRequestException>(() => Call(Client(chat, options)));

        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_EmptyContentWithReasoning_RetriesWhenEnabled()
    {
        var chat = new ScriptedChatClient()
            .Reply(Response(new TextReasoningContent("thinking only")))
            .Reply("final");
        var options = new ChatClientLlmOptions { Retry = new RetryOptions { RetryOnEmptyContent = true } };

        var response = await Call(Client(chat, options));

        Assert.Equal("final", response.Content);
        Assert.Equal(2, chat.Calls.Count);
    }

    [Fact]
    public async Task CallAsync_EmptyContentRetryExhausted_ThrowsEmptyContentException()
    {
        var chat = new ScriptedChatClient();
        for (var i = 0; i < 4; i++)
        {
            chat.Reply(Response(new TextReasoningContent("thinking only")));
        }

        var options = new ChatClientLlmOptions { Retry = new RetryOptions { RetryOnEmptyContent = true } };

        var error = await Assert.ThrowsAsync<EmptyContentException>(() => Call(Client(chat, options)));

        Assert.Equal("thinking only", error.Reasoning);
        Assert.Equal(4, chat.Calls.Count);
    }

    [Fact]
    public async Task CallAsync_EmptyContentWithReasoning_IsReturnedWhenRetryIsOff()
    {
        var chat = new ScriptedChatClient().Reply(Response(new TextReasoningContent("thinking only")));

        var response = await Call(Client(chat));

        Assert.Equal("", response.Content);
        Assert.Equal("thinking only", response.Reasoning);
        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_EmptyContentWithoutReasoning_IsNotRetried()
    {
        var chat = new ScriptedChatClient().Reply(Response());
        var options = new ChatClientLlmOptions { Retry = new RetryOptions { RetryOnEmptyContent = true } };

        await Call(Client(chat, options));

        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_ToolCallsWithReasoning_BypassEmptyContentRetry()
    {
        var chat = new ScriptedChatClient().Reply(Response(
            new TextReasoningContent("plan"),
            new FunctionCallContent("c1", "t", new Dictionary<string, object?>())));
        var options = new ChatClientLlmOptions { Retry = new RetryOptions { RetryOnEmptyContent = true } };

        var response = await Call(Client(chat, options));

        Assert.Single(response.ToolCalls);
        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_SlowAttempt_TimesOutAndIsRetriedAsATimeout()
    {
        var chat = new ScriptedChatClient()
            .Then(async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new ChatResponse();
            })
            .Reply("after timeout");
        var options = new ChatClientLlmOptions { RequestTimeout = TimeSpan.FromMilliseconds(50) };

        var response = await Call(Client(chat, options));

        Assert.Equal("after timeout", response.Content);
        Assert.Equal(2, chat.Calls.Count);
    }

    [Fact]
    public async Task CallAsync_AttemptsAlwaysTimeOut_ThrowTimeoutException()
    {
        var chat = new ScriptedChatClient();
        for (var i = 0; i < 2; i++)
        {
            chat.Then(async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new ChatResponse();
            });
        }

        var options = new ChatClientLlmOptions
        {
            RequestTimeout = TimeSpan.FromMilliseconds(30),
            Retry = new RetryOptions { MaxRetries = 1 },
        };

        await Assert.ThrowsAsync<TimeoutException>(() => Call(Client(chat, options)));
    }

    [Fact]
    public async Task CallAsync_CallerCancellation_ThrowsOperationCanceledWithoutRetry()
    {
        using var cts = new CancellationTokenSource();
        var chat = new ScriptedChatClient().Then(async token =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            return new ChatResponse();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(chat).CallAsync(_hello, cancellationToken: cts.Token));

        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task CallAsync_WithLogger_NeverLogsMessageContent()
    {
        var logger = new CapturingLogger();
        var chat = new ScriptedChatClient()
            .Fail(new HttpRequestException("secret-answer-text", null, HttpStatusCode.BadGateway))
            .Reply("secret-reply-text");
        var retry = new RetryPolicy(new RetryOptions(), (_, _) => Task.CompletedTask, () => 0.0, logger);
        var client = new ChatClientLlmClient(chat, retry: retry, logger: logger);

        await client.CallAsync([LlmMessage.User("secret-prompt-text")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain("secret", logger.Text);
    }

    [Fact]
    public void ContextWindowAndCountTokens_FollowOptionsAndApproximation()
    {
        var client = new ChatClientLlmClient(new ScriptedChatClient(), new ChatClientLlmOptions { ContextWindow = 1000 });

        Assert.Equal(1000, client.ContextWindow);
        Assert.Equal(2, client.CountTokens("12345678"));
    }

    [Fact]
    public void Constructor_InvalidOptions_Throws()
    {
        Assert.Throws<Nooa.Errors.ConfigValidationException>(() =>
            new ChatClientLlmClient(new ScriptedChatClient(), new ChatClientLlmOptions { MaxOutputTokens = 0 }));
    }

    [Fact]
    public async Task CallAsync_NullMessages_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Client(new ScriptedChatClient()).CallAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }
}
