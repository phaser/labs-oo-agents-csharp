// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/02_structured_outputs.py

namespace Nooa.Examples.Quickstart.Example02;

/// <summary>Quickstart 02: a typed return value. The runtime validates the answer and retries when it does not match.</summary>
public sealed class StructuredOutputExample : IQuickstartExample
{
    public int Number => 2;

    public string Title => "Structured output";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var agent = new StructuredFeedbackAgent(new AgentOptions { Llm = llm });
        var result = await agent.AnalyzeFeedbackAsync("Broken feature, needs immediate fix!");
        await output.WriteLineAsync($"Sentiment: {result.Sentiment}");
        await output.WriteLineAsync($"Topics: {string.Join(", ", result.Topics)}");
        await output.WriteLineAsync($"Urgency: {result.Urgency}");
        await output.WriteLineAsync($"Summary: {result.Summary}");
        await output.WriteLineAsync($"Confidence: {result.Confidence}");
    }
}
