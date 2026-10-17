// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Nooa.Llm;
using OpenAI;

namespace Nooa.Tests.Llm;

public class OpenAiChatClientFactoryTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"r","object":"chat.completion","created":1,"model":"m","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"ok"}}]}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    [Theory]
    [InlineData("openai/gpt-5", "gpt-5", null)]
    [InlineData("hosted_vllm/Qwen/Qwen2.5", "Qwen/Qwen2.5", null)]
    [InlineData("nvidia_nim/nvidia/nemotron", "nvidia/nemotron", "https://integrate.api.nvidia.com/v1")]
    [InlineData("gpt-5-mini", "gpt-5-mini", null)]
    [InlineData("anthropic/claude", "anthropic/claude", null)]
    public void ParseModel_StripsOnlyKnownRoutePrefixes(string input, string model, string? apiBase)
    {
        Assert.Equal((model, apiBase), OpenAiChatClientFactory.ParseModel(input));
    }

    [Fact]
    public async Task Create_SendsKeyAndModelToTheNamedHostOnly()
    {
        var handler = new CaptureHandler();
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        var chat = OpenAiChatClientFactory.Create("my-model", "https://gw.example.com/v1", "sk-key", options);

        await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { ModelId = "my-model" }, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("gw.example.com", request.RequestUri!.Host);
        Assert.EndsWith("/v1/chat/completions", request.RequestUri.AbsolutePath);
        Assert.Equal("Bearer sk-key", request.Headers.Authorization!.ToString());
        Assert.Contains("\"model\":\"my-model\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task Create_NoBase_UsesTheDefaultOpenAiHost()
    {
        var handler = new CaptureHandler();
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        var chat = OpenAiChatClientFactory.Create("m", null, "sk-key", options);

        await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { ModelId = "m" }, TestContext.Current.CancellationToken);

        Assert.Equal("api.openai.com", handler.Requests[0].RequestUri!.Host);
    }

    [Fact]
    public async Task Create_ReasoningEffort_IsOnTheWire()
    {
        var handler = new CaptureHandler();
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        var chat = OpenAiChatClientFactory.Create("m", "https://gw.example.com/v1", "k", options);
        var client = new ChatClientLlmClient(chat, new ChatClientLlmOptions { Model = "m", ReasoningEffort = ReasoningEffort.High, Temperature = 0.5f, Retry = null });

        await client.CallAsync([LlmMessage.User("hi")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("\"reasoning_effort\":\"high\"", handler.Bodies[0]);
        Assert.Contains("\"temperature\":0.5", handler.Bodies[0]);
    }
}
