// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/06_tracing.py

using Nooa.Tracing;

namespace Nooa.Examples.Quickstart.Example06;

/// <summary>Quickstart 06: every method type is traced, with parent and child spans, into a JSONL file.</summary>
/// <param name="traceDirectory">The directory for the trace file. The directory is created when it does not exist.</param>
public sealed class TracingExample(string traceDirectory = "traces/quickstart-06") : IQuickstartExample
{
    /// <summary>The trace session id. It is also the name of the trace file, without the extension.</summary>
    public const string SessionId = "quickstart-06";

    public int Number => 6;

    public string Title => "Tracing";

    /// <summary>The path of the trace file that a run writes.</summary>
    public string TraceFile => Path.Combine(traceDirectory, SessionId + ".jsonl");

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        NooaTracing.Enable(new JsonlSpanExporter(traceDirectory));
        try
        {
            using var session = TraceSession.Scope(SessionId);
            var agent = new MathAgent(new AgentOptions { Llm = new TracingLlmClient(llm) });
            var result = await agent.RunAsync("(10 + 5) * 2");
            await output.WriteLineAsync($"Result: {result}");
        }
        finally
        {
            NooaTracing.Flush();
            NooaTracing.Shutdown();
        }

        await output.WriteLineAsync("Spans captured:");
        await output.WriteLineAsync("  - RunAsync()        orchestrator, root span");
        await output.WriteLineAsync("  - CalculateAsync()  generation method, child of RunAsync()");
        await output.WriteLineAsync("  - ExplainAsync()    generation method, child of RunAsync()");
        await output.WriteLineAsync($"Trace file: {TraceFile}");
    }
}
