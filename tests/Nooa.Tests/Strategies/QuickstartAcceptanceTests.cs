// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/01_first_generation_method.py and 02_structured_outputs.py

using Nooa.Llm;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

/// <summary>
/// Quickstart 01 and 02 as offline tests: an agent class compiled by the generator, scripted model answers.
/// Quickstart 01 has no strategy attribute. The default is CodeAct (T17), so the test
/// sets Predict as the default strategy explicitly to keep a one-shot answer.
/// </summary>
public class QuickstartAcceptanceTests
{
    private static IReadOnlyList<string> Contents(FakeLlmCall call) =>
        [.. call.Messages.OfType<JsonMessage>().Select(m => m.Value["content"]!.GetValue<string>())];

    [Fact]
    public async Task Quickstart01_FirstGenerationMethod_ReturnsTheModelAnswerWithPromptsFromTheDocComments()
    {
        var fake = new FakeLlmClient().EnqueueText("{\"result\": \"Positive tone, but slow shipping.\"}");
        var agent = new FeedbackAgent(new AgentOptions { Llm = fake, DefaultStrategy = new PredictStrategy() });

        var result = await agent.AnalyzeFeedbackAsync("Great product, but shipping was slow");

        Assert.Equal("Positive tone, but slow shipping.", result);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(typeof(string), call.OutputType);
        var contents = Contents(call);
        var system = contents[0];
        Assert.Contains("<system_prompt>\nYou are an agent specializing in analyzing customer feedback.\n</system_prompt>", system, StringComparison.Ordinal);
        var task = contents.Single(c => c.Contains("# Your task", StringComparison.Ordinal));
        Assert.Contains("# Your task\nAnalyze customer feedback for sentiment and key topics in one sentence.", task, StringComparison.Ordinal);
        Assert.Contains("## Input parameters:\ntext = ", task, StringComparison.Ordinal);
        Assert.Contains("Great product, but shipping was slow", task, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart02_StructuredOutput_ReturnsATypedResult()
    {
        var fake = new FakeLlmClient().EnqueueText(
            """
            {"sentiment": "negative", "topics": ["feature"], "urgency": "high", "summary": "A feature is broken.", "confidence": 0.9}
            """);
        var agent = new StructuredFeedbackAgent(new AgentOptions { Llm = fake });

        var result = await agent.AnalyzeFeedbackAsync("Broken feature, needs immediate fix!");

        Assert.Equal("A feature is broken.", result.Summary);
        Assert.Equal("negative", result.Sentiment);
        Assert.Equal(["feature"], result.Topics);
        Assert.Equal("high", result.Urgency);
        Assert.Equal(0.9, result.Confidence);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(typeof(FeedbackAnalysis), call.OutputType);
        var contents = Contents(call);
        Assert.Contains("Agent for analyzing customer feedback with structured output.", contents[0], StringComparison.Ordinal);
        var task = contents.Single(c => c.Contains("# Your task", StringComparison.Ordinal));
        Assert.Contains("# Your task\nAnalyze customer feedback comprehensively.", task, StringComparison.Ordinal);
        Assert.Contains("Broken feature, needs immediate fix!", task, StringComparison.Ordinal);
        Assert.Contains(contents, c => c.Contains("<strategy_prompt>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quickstart02_InvalidThenValidAnswer_RetriesOnce()
    {
        var fake = new FakeLlmClient()
            .EnqueueText("""{"sentiment": "negative", "topics": "feature", "urgency": "high", "summary": "s", "confidence": 0.9}""")
            .EnqueueText("""{"sentiment": "negative", "topics": ["feature"], "urgency": "high", "summary": "s", "confidence": 0.9}""");
        var agent = new StructuredFeedbackAgent(new AgentOptions { Llm = fake });

        var result = await agent.AnalyzeFeedbackAsync("Broken");

        Assert.Equal(["feature"], result.Topics);
        Assert.Equal(2, fake.CallCount);
        Assert.Contains(Contents(fake.Calls[1]), c => c.Contains("Field `$.topics`", StringComparison.Ordinal));
    }
}
