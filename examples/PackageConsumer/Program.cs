// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

// Usage: dotnet run
// Offline by default: a FakeLlmClient answers. Set NVIDIA_API_KEY, OPENAI_API_KEY or NVIDIA_INFERENCE_API_KEY to call a real model (costs money).
// For another OpenAI-compatible endpoint, also set OPENAI_BASE_URL (https, or http to localhost) and NOOA_MODEL.

using Nooa.Llm;
using PackageConsumer;

static bool HasKey() =>
    new[] { "NVIDIA_API_KEY", "OPENAI_API_KEY", "NVIDIA_INFERENCE_API_KEY" }
        .Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));

ILlmClient llm;
if (HasKey())
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

var agent = new FeedbackAgent(new Nooa.AgentOptions { Llm = llm });
var result = await agent.AnalyzeAsync("Great product, but shipping was slow");
Console.WriteLine($"Sentiment: {result.Sentiment}");
Console.WriteLine($"Summary: {result.Summary}");
