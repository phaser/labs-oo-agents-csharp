// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.ClientModel;
using Microsoft.Extensions.AI;
using Nooa.Llm;
using OpenAI;

namespace Nooa.Tests.Llm;

/// <summary>
/// Calls a real OpenAI-compatible endpoint. Set NOOA_LIVE_BASE_URL, NOOA_LIVE_API_KEY and NOOA_LIVE_MODEL to run it.
/// Without them the test returns at once. Run only these tests with the filter Category=Live.
/// </summary>
[Trait("Category", "Live")]
public class ChatClientLlmClientLiveTests
{
    private sealed record Answer(int Value);

    [Fact]
    public async Task CallAsync_RealEndpoint_ReturnsTextAndStructuredOutput()
    {
        var baseUrl = Environment.GetEnvironmentVariable("NOOA_LIVE_BASE_URL");
        var apiKey = Environment.GetEnvironmentVariable("NOOA_LIVE_API_KEY");
        var model = Environment.GetEnvironmentVariable("NOOA_LIVE_MODEL");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            Assert.Skip("Set NOOA_LIVE_BASE_URL, NOOA_LIVE_API_KEY and NOOA_LIVE_MODEL to run this test");
        }

        var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
        var client = new ChatClientLlmClient(
            openAi.GetChatClient(model).AsIChatClient(),
            new ChatClientLlmOptions { Model = model, MaxOutputTokens = 512 });

        var text = await client.CallAsync(
            [LlmMessage.User("Reply with the single word: pong")],
            cancellationToken: TestContext.Current.CancellationToken);
        var structured = await client.CallAsync(
            [LlmMessage.User("What is 6 times 7? Answer as JSON.")],
            outputType: typeof(Answer),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("pong", text.Content, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(text.Usage);
        Assert.Equal(new Answer(42), structured.Parsed);
    }
}
