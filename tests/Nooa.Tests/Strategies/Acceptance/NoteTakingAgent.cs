// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart (a copy of the example type for the unit tests; the example has its own in examples/Quickstart)

using Nooa.Runtime;

namespace Nooa.Tests.Strategies.Acceptance;

/// <summary>Agent that stores notes and answers questions about them.</summary>
public partial class NoteTakingAgent(AgentOptions? options = null) : Agent(options)
{
    private readonly List<string> _notes = [];

    /// <summary>Exposes context management to the model (upstream: <c>spec(self, "context", hidden=False)</c>).</summary>
    public new ContextApi Context => base.Context;

    /// <summary>Add a note to the collection.</summary>
    public void AddNote(string text) => _notes.Add(text);

    /// <summary>Render all stored notes as a formatted list.</summary>
    public string RenderNotes() => _notes.Count == 0 ? "No notes yet." : string.Join("\n", _notes.Select(n => $"- {n}"));

    /// <summary>Store this note using AddNote and confirm what was saved.</summary>
    [Agentic]
    public partial Task<string> RecordAsync(string note);

    /// <summary>Answer the question using the notes visible in your context.</summary>
    [Agentic]
    public partial Task<string> AnswerAsync(string question);
}
