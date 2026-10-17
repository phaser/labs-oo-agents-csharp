// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/04_strategies.py

using Nooa.Config;
using Nooa.Strategies;

namespace Nooa.Examples.Quickstart.Example04;

/// <summary>Quickstart 04: PredictStrategy (one structured answer) against CodeActStrategy (a loop that runs code).</summary>
/// <param name="cwd">The working directory of the shell that the agent owns.</param>
public sealed class StrategiesExample(string cwd = ".") : IQuickstartExample
{
    private static readonly string[] _requests =
    [
        "What is the current working directory?",
        "List the files in the current directory.",
    ];

    public int Number => 4;

    public string Title => "Strategies";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var options = new AgentOptions
        {
            Llm = llm,
            Strategies = [new CodeActStrategy(new CodeActConfig { MaxIterations = 10 })],
        };
        var agent = new AnalysisAgent(options, cwd);
        try
        {
            const string sentence = "I love this product! Best purchase ever!";
            var sentiment = await agent.ClassifySentiment(sentence);
            await output.WriteLineAsync($"Sentence: {sentence}");
            await output.WriteLineAsync($"Sentiment: {sentiment}");

            foreach (var request in _requests)
            {
                var response = await agent.PerformTask(request);
                await output.WriteLineAsync($"Request: {request}");
                await output.WriteLineAsync($"Response: {response}");
            }
        }
        finally
        {
            await agent.CloseAsync();
        }
    }
}
