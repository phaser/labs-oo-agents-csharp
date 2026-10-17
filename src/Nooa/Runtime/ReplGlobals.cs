// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py

using Nooa.Agentdoc;

namespace Nooa.Runtime;

/// <summary>
/// The members that a cell sees without a qualifier: <c>Out</c>, <c>Console</c>, <c>Print</c>, <c>PPrint</c>, <c>Doc</c>,
/// <c>ReturnResult</c> and <c>Cancellation</c>. <see cref="ReplGlobals{TAgent}"/> adds <c>self</c>.
/// </summary>
/// <remarks>
/// The public members are visible to model code. A model that writes the same name in its own code gets its own
/// variable, which hides the member. The class is part of the guardrail and not of a security boundary (D12).
/// </remarks>
public abstract class ReplGlobals
{
    private object? _returned;
    private bool _hasReturned;

    /// <summary>Creates the globals. The engine fills the writers of <see cref="Console"/> for each cell.</summary>
    protected ReplGlobals()
    {
    }

    /// <summary>The console of the running cell.</summary>
    public ReplConsole Console { get; } = new();

    /// <summary>Values of earlier cells: <c>Out[n]</c>.</summary>
    public OutAccessor Out { get; } = new();

    /// <summary>
    /// The token of the running cell. It is cancelled at the time limit. Pass it to long calls.
    /// A loop that never checks it cannot be stopped inside the process (backlog item B01).
    /// </summary>
    public CancellationToken Cancellation { get; internal set; }

    /// <summary>The raw argument values. The engine reads it in the generated declarations. Model code must not use it.</summary>
    public IReadOnlyDictionary<string, object?> __args { get; internal set; } = new Dictionary<string, object?>();

    internal bool HasReturned => _hasReturned;

    internal object? ReturnedValue => _returned;

    /// <summary>Writes a value and a line end to the standard output of the cell.</summary>
    public void Print(object? value) => Console.WriteLine(value);

    /// <summary>Writes the formatted value (see <see cref="PFormat"/>) and a line end to the standard output of the cell.</summary>
    public void PPrint(object? value)
    {
        PFormat.Write(Console.Out, value);
        Console.Out.WriteLine();
    }

    /// <summary>Writes the documentation of a type, a method or an instance to the standard output of the cell.</summary>
    /// <param name="obj">What to document.</param>
    /// <param name="concise">Show only the first line of each doc comment.</param>
    public void Doc(object obj, bool concise = false) => Console.Out.WriteLine(AgentDocs.Doc(obj, concise));

    /// <summary>
    /// Ends the cell and gives <paramref name="value"/> to the caller. The call throws, so the code after it does not run.
    /// </summary>
    public void ReturnResult(object? value = null)
    {
        _returned = value;
        _hasReturned = true;
        throw new ReplReturnSignal();
    }

    internal void BeginCell(CancellationToken cancellation)
    {
        Cancellation = cancellation;
        _returned = null;
        _hasReturned = false;
    }
}
