// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/03_codeact_tools.py and 04_strategies.py

using Nooa.Config;
using Nooa.Llm;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

/// <summary>Quickstart 03 and 04 as offline tests: agent classes compiled by the generator, scripted model answers, CodeAct as the default.</summary>
public class CodeActAcceptanceTests
{
    private const string _inventoryCell =
        """
        var total = 0.0;
        var unavailable = new List<string>();
        foreach (var item in items)
        {
            if (self.GetStock(item) > 0) total += self.GetPrice(item);
            else unavailable.Add(item);
        }
        ReturnResult(new OrderResult(unavailable.Count == 0 && total <= budget, total, unavailable));
        """;

    private static IReadOnlyList<string> Contents(FakeLlmCall call) =>
        [.. call.Messages.OfType<JsonMessage>().Select(m => m.Value["content"]?.GetValue<string>() ?? "")];

    [Fact]
    public async Task Quickstart03_CodeActIsTheDefault_RunsACellAndReturnsTheTypedResult()
    {
        var fake = new FakeLlmClient().EnqueueToolCall("execute_csharp", new { code = _inventoryCell });
        var agent = new InventoryAgent(new AgentOptions { Llm = fake });

        var result = await agent.CanFulfillOrder(["apple", "banana", "orange"], 5.0);

        Assert.False(result.CanFulfill);
        Assert.Equal(1.25, result.TotalCost);
        Assert.Equal(["orange"], result.UnavailableItems);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(["execute_csharp", "return_result"], call.Tools!.Select(t => t.Name));
        var system = Contents(call)[0];
        Assert.Contains("You are an agent that checks inventory", system, StringComparison.Ordinal);
        Assert.Contains("Jupyter-like C# session", system, StringComparison.Ordinal);
        Assert.Contains("GetStock", system, StringComparison.Ordinal);
        var all = string.Join('\n', Contents(call));
        Assert.Contains("Task: CanFulfillOrder()", all, StringComparison.Ordinal);
        Assert.Contains("items (List<string>):", all, StringComparison.Ordinal);
        Assert.Contains("record OrderResult", all, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart03_ModelCallsAMethodFirst_SeesTheOutputOnTheNextTurn()
    {
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "Print(self.GetStock(\"apple\"));" }, id: "call_1")
            .EnqueueToolCall("execute_csharp", new { code = _inventoryCell }, id: "call_2");
        var agent = new InventoryAgent(new AgentOptions { Llm = fake });

        var result = await agent.CanFulfillOrder(["apple"], 5.0);

        Assert.True(result.CanFulfill);
        Assert.Equal(2, fake.CallCount);
        Assert.Contains(Contents(fake.Calls[1]), c => c.Contains("50", StringComparison.Ordinal) && c.Contains("CellOutput", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quickstart04_CodeActWithMaxIterations_UsesTheShell()
    {
        using var directory = new TempDirectory();
        var cwd = directory.Path;
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "var r = await self.Shell.RunAsync(\"pwd\"); ReturnResult(r.Stdout.Trim());" });
        var strategy = new CodeActStrategy(new CodeActConfig { MaxIterations = 10 });
        var agent = new AnalysisAgent(new AgentOptions { Llm = fake, Strategies = [strategy] }, cwd);

        var response = await agent.PerformTask("What is the current working directory?");

        Assert.EndsWith(Path.GetFileName(cwd), response, StringComparison.Ordinal);
        await agent.CloseAsync();
    }
}
