// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Examples.Quickstart;
using Nooa.Examples.Quickstart.Example04;
using Nooa.Examples.Quickstart.Example06;
using Nooa.Tracing;
using Nooa.Util;

namespace Nooa.Tests.Acceptance;

/// <summary>
/// Runs quickstart examples 01 to 08 against a real model. This costs money.
/// Set NVIDIA_API_KEY, OPENAI_API_KEY or NVIDIA_INFERENCE_API_KEY to run it. Without a key each test is skipped.
/// Run only these tests with the filter Category=Live.
/// Example 04 runs the model shell in a temporary directory. Example 06 writes its trace to a temporary directory.
/// </summary>
[Trait("Category", "Live")]
[Collection(AgentStaticCollection.Name)]
public sealed class QuickstartLiveTests : IDisposable
{
    public QuickstartLiveTests() => NooaTracing.Shutdown();

    public void Dispose() => NooaTracing.Shutdown();

    public static TheoryData<int> Numbers => [1, 2, 3, 4, 5, 6, 7, 8];

    [Theory]
    [MemberData(nameof(Numbers))]
    public async Task Example_RealModel_CompletesAndPrintsAnAnswer(int number)
    {
        if (!HasCredential())
        {
            Assert.Skip("Set NVIDIA_API_KEY, OPENAI_API_KEY or NVIDIA_INFERENCE_API_KEY to run this test");
        }

        using var directory = new TempDirectory();
        var (_, llm) = Quickstart.CreateDefaultLlm();
        IQuickstartExample example = number switch
        {
            4 => new StrategiesExample(directory.Path),
            6 => new TracingExample(directory.Path),
            _ => QuickstartExamples.CreateAll().Single(e => e.Number == number),
        };
        var output = new StringWriter();

        await example.RunAsync(llm, output);

        Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
    }

    private static bool HasCredential() =>
        new[] { "NVIDIA_API_KEY", "OPENAI_API_KEY", "NVIDIA_INFERENCE_API_KEY", "NVIDIA_INTERNAL_API_KEY" }
            .Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));
}
