// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/base.py

using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// Base class of every generation strategy. A strategy knows how to turn one <see cref="CurrentCall"/> into a result:
/// it builds the prompt, calls the model through <see cref="IRuntimeServices"/>, processes the response and retries.
/// </summary>
/// <remarks>
/// The runtime resolves the strategy of a call in this order: a call-level override (<see cref="Agent.UseStrategy"/>),
/// the method <see cref="StrategyAttribute"/>, then the agent default strategy. A strategy type from the attribute
/// needs a public parameterless constructor, unless the agent options supply an instance.
/// Context block overrides and block order (upstream <c>get_block_overrides</c>, <c>get_block_order</c>) arrive with
/// the context renderer (T13).
/// </remarks>
public abstract class GenerationStrategy
{
    /// <summary>The strategy name. It defaults to the class name.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// Whether generation hooks fire for this strategy. Set it to false for a strategy that makes no model call,
    /// to keep the traces quiet.
    /// </summary>
    public virtual bool Traceable => true;

    /// <summary>
    /// Whether the strategy needs exclusive generation access. The default is true, so concurrent model calls of
    /// one agent do not interleave their events. Return false for a stateless strategy that can run concurrently.
    /// </summary>
    public virtual bool RequiresLock => true;

    /// <summary>
    /// Context blocks that this strategy replaces, adds (a value) or removes (null). The override applies to every
    /// request of the strategy. A value can be plain, a <see cref="Nooa.ContextBlocks.Context"/> or a
    /// <see cref="Nooa.ContextBlocks.DynamicContext"/>. The default is none.
    /// </summary>
    public virtual IReadOnlyDictionary<string, object?>? GetBlockOverrides() => null;

    /// <summary>The keys of blocks that this strategy adds to the static (cacheable) partition. The default is none.</summary>
    public virtual IReadOnlySet<string>? GetStaticBlockKeys() => null;

    /// <summary>
    /// The keys of the system blocks that come first, in this order. Other blocks follow in their own order.
    /// The default keeps the block order.
    /// </summary>
    public virtual IReadOnlyList<string>? GetBlockOrder() => null;

    /// <summary>Runs the strategy for one call and returns its result.</summary>
    /// <param name="runtime">The services of the agent runtime.</param>
    /// <param name="call">The call to generate.</param>
    /// <param name="cancellationToken">Cancels the generation.</param>
    /// <exception cref="Nooa.Errors.GenerationException">The generation fails after the retries.</exception>
    /// <exception cref="Nooa.Errors.GenerationAbortedException">The agent aborts the generation.</exception>
    public abstract Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken);
}
