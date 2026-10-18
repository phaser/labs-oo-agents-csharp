// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

// Usage: dotnet run
// Offline by default: a FakeLlmClient answers. Set OPENROUTER_API_KEY to call DeepSeek V4.1 Flash 1017 through OpenRouter,
// or NVIDIA_API_KEY / OPENAI_API_KEY / NVIDIA_INFERENCE_API_KEY to use the default routes (costs money).
// Set NOOA_MODEL to replace the model of the selected route.

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Nooa;
using Nooa.Llm;
using OpenAI;
using PackageConsumer;

const string openRouterBaseUrl = "https://openrouter.ai/api/v1";
const string openRouterModel = "deepseek/deepseek-v4.1-flash-1017";

// Fallback prices for deepseek/deepseek-v4.1-flash-1017 in USD per token. OpenRouter bills the upstream provider it
// picks, so use its reported usage.cost when present; these list prices only estimate the cost when it is not.
// prompt $0.15 / 1M, completion $0.60 / 1M, cached input $0.003 / 1M.
const double promptUsdPerToken = 0.00000015;
const double completionUsdPerToken = 0.0000006;
const double cachedInputUsdPerToken = 0.000000003;

static bool HasKey() =>
    new[] { "OPENROUTER_API_KEY", "NVIDIA_API_KEY", "OPENAI_API_KEY", "NVIDIA_INFERENCE_API_KEY" }
        .Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));

static (string Model, ILlmClient Client)? OpenRouterClient()
{
    if (Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") is not { Length: > 0 } key)
    {
        return null;
    }

    var model = Environment.GetEnvironmentVariable("NOOA_MODEL") is { Length: > 0 } custom ? custom : openRouterModel;
    var chat = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = new Uri(openRouterBaseUrl) })
        .GetChatClient(model)
        .AsIChatClient();
    return (model, new ChatClientLlmClient(chat, new ChatClientLlmOptions
    {
        Model = model,
        MaxOutputTokens = 2048,
        ReasoningEffort = ReasoningEffort.Low,
    }));
}

ILlmClient llm;
if (OpenRouterClient() is { } openRouter)
{
    Console.WriteLine($"Model: {openRouter.Model}");
    llm = openRouter.Client;
}
else if (HasKey())
{
    var (model, client) = Nooa.Util.Quickstart.CreateDefaultLlm();
    Console.WriteLine($"Model: {model}");
    llm = client;
}
else
{
    Console.WriteLine("No API key set: using FakeLlmClient.");
    llm = new FakeLlmClient().EnqueueText("""{"sentiment": "mixed", "summary": "Good product, slow shipping."}""");
}

var agent = new FeedbackAgent(new AgentOptions { Llm = llm });
var result = await agent.AnalyzeAsync("Great product, but shipping was slow");
Console.WriteLine($"Sentiment: {result.Sentiment}");
Console.WriteLine($"Summary: {result.Summary}");

var responses = agent.Events.Query(type: "LLMResponse").OfType<LlmResponse>().ToList();
var usages = responses.Select(response => response.Usage).OfType<LlmUsage>().ToList();

if (usages.Count == 0)
{
    Console.WriteLine("Cost: no usage reported");
}
else
{
    var input = usages.Sum(usage => usage.InputTokens);
    var cached = usages.Sum(usage => Math.Min(usage.CachedInputTokens, usage.InputTokens));
    var output = usages.Sum(usage => usage.OutputTokens);
    var reported = responses.Sum(ProviderCost);

    var cost = reported > 0
        ? reported
        : (input - cached) * promptUsdPerToken + cached * cachedInputUsdPerToken + output * completionUsdPerToken;

    Console.WriteLine($"Tokens: {input} input ({cached} cached), {output} output");
    Console.WriteLine(reported > 0 ? $"Cost: ${cost:F6}" : $"Estimated cost: ${cost:F6}");
}

static double ProviderCost(LlmResponse response)
{
    if (response.Usage is { CostUsd: > 0 } usage)
    {
        return usage.CostUsd;
    }

    if (response.RawResponse is null)
    {
        return 0;
    }

    var json = ModelReaderWriter.Write(response.RawResponse, new ModelReaderWriterOptions("J"));
    using var document = JsonDocument.Parse(json);
    return document.RootElement.TryGetProperty("usage", out var usageElement)
        && usageElement.TryGetProperty("cost", out var costElement)
        && costElement.TryGetDouble(out var cost)
            ? cost
            : 0;
}
