// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

/// <summary>
/// Measures memory growth per cell (plan risk: Roslyn script assemblies are not collectible).
/// It runs only when the environment variable NOOA_BENCHMARK is 1, so CI skips it.
/// </summary>
[Trait("Category", "Benchmark")]
public class RoslynExecutionEngineMemoryTests
{
    [Fact]
    public async Task ExecuteAsync_500TrivialCells_ReportsMemoryGrowth()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("NOOA_BENCHMARK") == "1", "Set NOOA_BENCHMARK=1 to run.");
        var engine = new RoslynExecutionEngine(new ReplGlobals<ReplAgent>(new ReplAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() })));
        var token = TestContext.Current.CancellationToken;
        await engine.ExecuteAsync("var warm = 0;", token);
        var rows = new List<string>();
        var watch = Stopwatch.StartNew();
        var start = Snapshot();
        for (var i = 1; i <= 500; i++)
        {
            var result = await engine.ExecuteAsync($"var v{i} = {i}; v{i} + warm", token);
            Assert.Equal(Nooa.ContextBlocks.ResultStatus.Complete, result.Status);
            if (i % 100 == 0)
            {
                var now = Snapshot();
                rows.Add(string.Create(CultureInfo.InvariantCulture, $"cells={i} managed={now.Managed / 1_048_576.0:F1} MiB rss={now.Rss / 1_048_576.0:F1} MiB elapsed={watch.Elapsed.TotalSeconds:F1} s"));
            }
        }

        var end = Snapshot();
        rows.Add(string.Create(CultureInfo.InvariantCulture, $"start managed={start.Managed / 1_048_576.0:F1} MiB rss={start.Rss / 1_048_576.0:F1} MiB; per cell managed={(end.Managed - start.Managed) / 500 / 1024.0:F1} KiB"));
        TestContext.Current.SendDiagnosticMessage(string.Join("\n", rows));
    }

    private static (long Managed, long Rss) Snapshot()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return (GC.GetTotalMemory(true), Process.GetCurrentProcess().WorkingSet64);
    }
}
