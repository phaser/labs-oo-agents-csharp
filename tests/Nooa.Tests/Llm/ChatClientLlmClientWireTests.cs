// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Nooa.Llm;
using OpenAI;

namespace Nooa.Tests.Llm;

/// <summary>Runs the client through the real OpenAI SDK adapter against a stub transport (no network).</summary>
public class ChatClientLlmClientWireTests
{
    private sealed record Point(int X, int Y);

    private const string _toolCallResponse = """
        {"id":"r1","object":"chat.completion","created":1,"model":"gpt-test",
         "choices":[{"index":0,"finish_reason":"tool_calls","message":{"role":"assistant","content":null,
           "tool_calls":[{"id":"call_1","type":"function","function":{"name":"search","arguments":"{ \"q\" :  \"a b\" }"}}]}}],
         "usage":{"prompt_tokens":12,"completion_tokens":5,"total_tokens":17,
           "prompt_tokens_details":{"cached_tokens":8},"completion_tokens_details":{"reasoning_tokens":2}}}
        """;

    private const string _textResponse = """
        {"id":"r2","object":"chat.completion","created":1,"model":"gpt-test",
         "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{\"x\":1,\"y\":2}"}}],
         "usage":{"prompt_tokens":3,"completion_tokens":4,"total_tokens":7}}
        """;

    private static (ChatClientLlmClient Client, StubHttpHandler Handler) Create(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHttpHandler(responseJson, status);
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("http://localhost.invalid/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        var chat = new OpenAIClient(new ApiKeyCredential("test-key"), options).GetChatClient("gpt-test").AsIChatClient();
        return (new ChatClientLlmClient(chat, new ChatClientLlmOptions { MaxOutputTokens = 77, Retry = null }), handler);
    }

    private static readonly ToolDefinition _search = new("search", "Search", (JsonObject)JsonNode.Parse(
        """{"type":"object","properties":{"q":{"type":"string"}},"required":["q"]}""")!);

    [Fact]
    public async Task CallAsync_ToolCallResponse_KeepsTheExactArgumentString()
    {
        var (client, _) = Create(_toolCallResponse);

        var response = await client.CallAsync([LlmMessage.User("find")], [_search], cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(response.ToolCalls);
        Assert.Equal(("call_1", "search"), (call.Id, call.Name));
        Assert.Equal("{ \"q\" :  \"a b\" }", call.Arguments);
        Assert.Equal(FinishReason.ToolCalls, response.FinishReason);
        Assert.Equal("gpt-test", response.ModelName);
    }

    [Fact]
    public async Task CallAsync_ToolCallResponse_NormalizesUsage()
    {
        var (client, _) = Create(_toolCallResponse);

        var response = await client.CallAsync([LlmMessage.User("find")], [_search], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(12, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(8, response.Usage.CachedInputTokens);
        Assert.Equal(2, response.Usage.ReasoningTokens);
        Assert.Equal(17, response.Usage.TotalTokens);
    }

    [Fact]
    public async Task CallAsync_Request_CarriesBudgetStrictToolAndMessages()
    {
        var (client, handler) = Create(_toolCallResponse);

        await client.CallAsync(
            [LlmMessage.System("sys"), LlmMessage.User("find"), LlmMessage.Tool("call_0", "earlier result")],
            [_search],
            cancellationToken: TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(Assert.Single(handler.RequestBodies))!;
        Assert.Equal("gpt-test", body["model"]!.GetValue<string>());
        Assert.Equal(77, (body["max_completion_tokens"] ?? body["max_tokens"])!.GetValue<int>());
        var function = body["tools"]![0]!["function"]!;
        Assert.Equal("search", function["name"]!.GetValue<string>());
        Assert.True(function["strict"]!.GetValue<bool>());
        Assert.False(function["parameters"]!["additionalProperties"]!.GetValue<bool>());
        Assert.Equal("tool", body["messages"]![2]!["role"]!.GetValue<string>());
        Assert.Equal("call_0", body["messages"]![2]!["tool_call_id"]!.GetValue<string>());
        Assert.Equal("earlier result", body["messages"]![2]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task CallAsync_ToolCallHistory_IsSentAsAnAssistantToolCall()
    {
        var (client, handler) = Create(_textResponse);
        var earlier = new LlmResponse
        {
            Parts = [new ToolCall { Id = "call_9", Name = "search", Arguments = "{\"q\":\"x\"}" }],
            FinishReason = FinishReason.ToolCalls,
        };

        await client.CallAsync([LlmMessage.User("find"), new ResponseMessage(earlier), LlmMessage.Tool("call_9", "r")], cancellationToken: TestContext.Current.CancellationToken);

        var assistant = JsonNode.Parse(Assert.Single(handler.RequestBodies))!["messages"]![1]!;
        Assert.Equal("assistant", assistant["role"]!.GetValue<string>());
        Assert.Equal("call_9", assistant["tool_calls"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("search", assistant["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task CallAsync_StructuredOutput_SendsStrictJsonSchemaAndParses()
    {
        var (client, handler) = Create(_textResponse);

        var response = await client.CallAsync([LlmMessage.User("point")], outputType: typeof(Point), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new Point(1, 2), response.Parsed);
        var format = JsonNode.Parse(Assert.Single(handler.RequestBodies))!["response_format"]!;
        Assert.Equal("json_schema", format["type"]!.GetValue<string>());
        Assert.Equal("Point", format["json_schema"]!["name"]!.GetValue<string>());
        Assert.True(format["json_schema"]!["strict"]!.GetValue<bool>());
        Assert.False(format["json_schema"]!["schema"]!["additionalProperties"]!.GetValue<bool>());
    }

    [Fact]
    public async Task CallAsync_FreeFormOutput_SendsNonStrictJsonSchema()
    {
        var (client, handler) = Create(_textResponse);

        await client.CallAsync([LlmMessage.User("map")], outputType: typeof(Dictionary<string, int>), cancellationToken: TestContext.Current.CancellationToken);

        var format = JsonNode.Parse(Assert.Single(handler.RequestBodies))!["response_format"]!;
        Assert.False(format["json_schema"]!["strict"]!.GetValue<bool>());
    }

    [Fact]
    public async Task CallAsync_NativeState_IsKeptAsJsonWhenTheSdkObjectCanWriteItself()
    {
        var (client, _) = Create(_toolCallResponse);

        var response = await client.CallAsync([LlmMessage.User("find")], [_search], cancellationToken: TestContext.Current.CancellationToken);

        var native = Assert.Single(response.ToolCalls).Native;
        Assert.NotNull(native);
        Assert.Contains("call_1", native.ToJsonString());
    }

    [Fact]
    public async Task CallAsync_ServerError_SurfacesAnExceptionWithTheStatus()
    {
        var (client, _) = Create("""{"error":{"message":"overloaded"}}""", HttpStatusCode.BadGateway);

        var error = await Assert.ThrowsAsync<ClientResultException>(() =>
            client.CallAsync([LlmMessage.User("x")], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(502, error.Status);
        Assert.Equal((true, false), new RetryPolicy().Classify(error));
    }
}
