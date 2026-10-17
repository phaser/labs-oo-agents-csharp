// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/scoped.py

using Nooa.Runtime;

namespace Nooa.ContextBlocks;

/// <summary>
/// Overrides context blocks and event filtering inside a scope. The overrides apply to every LLM call inside the scope,
/// including nested calls and parallel tasks that start inside it. Scopes nest: an inner scope inherits the blocks of
/// the outer scope and can override them, and its event query replaces the outer one.
/// </summary>
/// <remarks>
/// Upstream also accepts a scope as an argument of the <c>@strategy</c> decorator. A C# attribute argument must be
/// a constant, so only the <see cref="Enter"/> form exists. The scope is held in an <see cref="AsyncLocal{T}"/> (decision D9).
/// </remarks>
/// <example>
/// <code>
/// using (new ScopedContext(new Dictionary&lt;string, object?&gt; { ["focus"] = "Analyze security only" }).Enter())
/// {
///     await agent.Analyze(data);   // the call sees the "focus" block
/// }
/// </code>
/// </example>
public sealed class ScopedContext(IReadOnlyDictionary<string, object?>? context = null, EventQuery? events = null)
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, object?>?> _blocks = new();
    private static readonly AsyncLocal<EventQuery?> _events = new();

    /// <summary>The block overrides of the innermost scope, with the blocks of the outer scopes merged in. Null outside a scope.</summary>
    internal static IReadOnlyDictionary<string, object?>? CurrentBlocks => _blocks.Value;

    /// <summary>The event query of the innermost scope that sets one. Null outside a scope.</summary>
    internal static EventQuery? CurrentEvents => _events.Value;

    /// <summary>
    /// The block overrides. A plain value sets the block, a <see cref="Context"/> or a <see cref="DynamicContext"/>
    /// sets it with its placement or provider, and null removes the block inside the scope.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Context { get; } = context;

    /// <summary>The event query that replaces the default event list. Null keeps the outer query.</summary>
    public EventQuery? Events { get; } = events;

    /// <summary>
    /// Enters the scope. Dispose the result to leave it. This method is not async on purpose: a scope that is set
    /// inside an async method would end when that method returns.
    /// </summary>
    public IDisposable Enter()
    {
        var previousBlocks = _blocks.Value;
        var previousEvents = _events.Value;
        var merged = new Dictionary<string, object?>(previousBlocks ?? new Dictionary<string, object?>());
        foreach (var (key, value) in Context ?? new Dictionary<string, object?>())
        {
            merged[key] = value;
        }

        _blocks.Value = merged;
        _events.Value = Events ?? previousEvents;
        return new Subscription(() =>
        {
            _blocks.Value = previousBlocks;
            _events.Value = previousEvents;
        });
    }
}
