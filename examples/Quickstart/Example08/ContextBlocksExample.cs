// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/08_context_blocks.py

using Nooa.ContextBlocks;

namespace Nooa.Examples.Quickstart.Example08;

/// <summary>Quickstart 08: pin live state into the system prompt with an expression block and a fixed block.</summary>
public sealed class ContextBlocksExample : IQuickstartExample
{
    private static readonly string[] _notes =
    [
        "Deploy uses blue-green strategy with 5-minute health checks.",
        "Database migrations run before traffic shifts.",
        "Rollback is automatic if error rate exceeds 1% for 2 minutes.",
    ];

    public int Number => 8;

    public string Title => "Context blocks";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var agent = new NoteTakingAgent(new AgentOptions { Llm = llm });

        // The expression is evaluated again on every model turn, so the model always sees the latest notes.
        agent.Context["notes"] = Context.Expr("self.RenderNotes()");
        foreach (var note in _notes)
        {
            await agent.RecordAsync(note);
        }

        var answer = await agent.AnswerAsync("What triggers an automatic rollback?");
        await output.WriteLineAsync($"Answer: {answer}");

        // A fixed block in the cache-friendly prefix. Use it for specs, plans and decisions.
        agent.Context["policy"] = Context.Of("Always prefer rollback over forward-fix during incidents.", prefix: true);
        var answer2 = await agent.AnswerAsync("Should we try to fix forward or roll back?");
        await output.WriteLineAsync($"Answer: {answer2}");
    }
}
