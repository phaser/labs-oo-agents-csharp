// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/03_codeact_tools.py

using System.Globalization;

namespace Nooa.Examples.Quickstart.Example03;

/// <summary>Quickstart 03: plain methods are tools for the model. CodeAct is the default strategy.</summary>
public sealed class CodeActToolsExample : IQuickstartExample
{
    public int Number => 3;

    public string Title => "CodeAct tools";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var agent = new InventoryAgent(new AgentOptions { Llm = llm });
        var result = await agent.CanFulfillOrder(["apple", "banana", "orange"], 5.0);
        await output.WriteLineAsync($"Can fulfill: {result.CanFulfill}");
        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Total cost: {result.TotalCost}"));
        await output.WriteLineAsync($"Unavailable items: {string.Join(", ", result.UnavailableItems)}");
    }
}
