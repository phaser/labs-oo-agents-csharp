// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/01_first_generation_method.py

namespace Nooa.Examples.Quickstart.Example01;

/// <summary>Quickstart 01: the simplest possible agent. One generation method, no strategy attribute.</summary>
public sealed class FirstGenerationMethodExample : IQuickstartExample
{
    public int Number => 1;

    public string Title => "First generation method";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var agent = new FeedbackAgent(new AgentOptions { Llm = llm });
        var result = await agent.AnalyzeFeedbackAsync("Great product, but shipping was slow");
        await output.WriteLineAsync(result);
    }
}
