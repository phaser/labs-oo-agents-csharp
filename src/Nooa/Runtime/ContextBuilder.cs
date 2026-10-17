// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context_builder.py

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// The pipeline that gathers, resolves and orders the context blocks. The phases run in order, and a later phase
/// overrides an earlier one:
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>The blocks of the <see cref="ContextManager"/>: the protected framework blocks and the user blocks.</item>
/// <item>The overrides of the strategy (<see cref="GenerationStrategy.GetBlockOverrides"/>).</item>
/// <item>The overrides of the active <see cref="ScopedContext"/>.</item>
/// <item>The block order of the strategy.</item>
/// <item>The active events as blocks with roles. The query priority is: runtime, scope, agent, no filter.</item>
/// </list>
/// <para>
/// The pipeline changes no state. The caller applies <see cref="BuildResult.ResolvedCache"/>. The provider of a block
/// that throws shows <c>ExceptionType: message</c> in the block, so one broken block does not stop the whole context.
/// Upstream evaluates Python expressions. C# calls the provider delegate (decision D5). The decorator context of
/// <c>@strategy(context=...)</c> is not ported: an attribute cannot hold a delegate. Use <see cref="ScopedContext"/>.
/// </para>
/// </remarks>
internal static class ContextBuilder
{
    /// <summary>Builds the complete block list from all sources.</summary>
    /// <param name="contextManager">The blocks of the agent. Read only.</param>
    /// <param name="eventManager">The events of the agent.</param>
    /// <param name="strategy">The current strategy, or null.</param>
    /// <param name="contextBlockFormat">The bounds for the values of context blocks.</param>
    /// <param name="scopedContext">The block overrides of the active scope.</param>
    /// <param name="runtimeEventQuery">The query set on the event manager. It has the highest priority.</param>
    /// <param name="scopedEventQuery">The query of the active scope.</param>
    /// <param name="agentEventQuery">The default query of the agent.</param>
    /// <param name="currentCallId">The id that <c>current</c> stands for in a query.</param>
    /// <param name="logger">Receives a warning for a provider that fails.</param>
    /// <param name="expressions">Binds the <see cref="Context.Expr"/> blocks of the overrides. Without it such a block throws.</param>
    public static BuildResult Build(
        ContextManager contextManager,
        EventManager eventManager,
        GenerationStrategy? strategy,
        FormatConfig? contextBlockFormat = null,
        IReadOnlyDictionary<string, object?>? scopedContext = null,
        EventQuery? runtimeEventQuery = null,
        EventQuery? scopedEventQuery = null,
        EventQuery? agentEventQuery = null,
        string? currentCallId = null,
        ILogger? logger = null,
        ContextExpressions? expressions = null)
    {
        ArgumentNullException.ThrowIfNull(contextManager);
        ArgumentNullException.ThrowIfNull(eventManager);
        logger ??= NullLogger.Instance;
        var resolvedCache = new Dictionary<string, object?>();
        var blocks = PersistentBlocks(contextManager, contextBlockFormat, resolvedCache, logger);
        var disabled = contextManager.Disabled();
        if (strategy?.GetBlockOverrides() is { Count: > 0 } overrides)
        {
            blocks = ApplyOverrides(blocks, overrides, contextBlockFormat, logger, strategy.GetStaticBlockKeys(), disabled, expressions);
        }

        if (scopedContext is { Count: > 0 })
        {
            blocks = ApplyOverrides(blocks, scopedContext, contextBlockFormat, logger, null, disabled, expressions);
        }

        blocks = Reorder(blocks, strategy?.GetBlockOrder());
        var query = runtimeEventQuery ?? scopedEventQuery ?? agentEventQuery;
        return new BuildResult([.. blocks, .. EventBlocks(eventManager, query, currentCallId)], resolvedCache);
    }

    /// <summary>Turns a value into block text. A string stays as it is when it fits, other values are printed within the bounds.</summary>
    internal static string FormatValue(object? value, FormatConfig? format) =>
        value is null ? "null" : PFormat.Format(value, format ?? new FormatConfig { MaxString = null, MaxLength = null, MaxDepth = null }, unquoteStrings: true);

    /// <summary>
    /// Calls the provider of a block. A failure becomes <c>ExceptionType: message</c> in the block, with a warning.
    /// A string result stays as it is, another result is printed within the bounds.
    /// </summary>
    private static string Resolve(string key, DynamicContext dynamic, FormatConfig? format, ILogger logger)
    {
        object? result;
        try
        {
            result = dynamic.Provider();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Context block {Key} failed to resolve: {ExceptionType}: {Message}", key, ex.GetType().Name, ex.Message);
            return $"{ex.GetType().Name}: {ex.Message}";
        }

        return result as string ?? FormatValue(result, format);
    }

    private static List<ResolvedBlock> PersistentBlocks(
        ContextManager contextManager, FormatConfig? format, Dictionary<string, object?> resolvedCache, ILogger logger)
    {
        var blocks = new List<ResolvedBlock>();
        foreach (var (key, entry) in contextManager.RawItems())
        {
            if (contextManager.IsDisabled(key))
            {
                continue;
            }

            var metadata = new BlockMetadata(UserBlock: !contextManager.IsProtected(key), Static: entry.IsStatic);
            string content;
            if (entry.Provider is { } provider)
            {
                content = Resolve(key, new DynamicContext(provider), format, logger);
                resolvedCache[key] = content;
            }
            else
            {
                content = FormatValue(entry.Value, format);
            }

            blocks.Add(new ResolvedBlock(key, content, Role.System, metadata));
        }

        return blocks;
    }

    /// <summary>
    /// Applies overrides to the blocks: a null value removes a block, any other value replaces the block or appends a
    /// new one. A replaced block keeps its partition, unless a <see cref="Context"/> sets one.
    /// </summary>
    private static List<ResolvedBlock> ApplyOverrides(
        List<ResolvedBlock> blocks,
        IReadOnlyDictionary<string, object?> overrides,
        FormatConfig? format,
        ILogger logger,
        IReadOnlySet<string>? staticKeys,
        IReadOnlySet<string> disabledKeys,
        ContextExpressions? expressions)
    {
        var index = new Dictionary<string, int>();
        for (var i = 0; i < blocks.Count; i++)
        {
            index.TryAdd(blocks[i].Key, i);
        }

        var replacements = new Dictionary<int, ResolvedBlock?>();
        var appends = new List<ResolvedBlock>();
        foreach (var (key, raw) in overrides)
        {
            if (raw is null || disabledKeys.Contains(key))
            {
                if (index.TryGetValue(key, out var removed))
                {
                    replacements[removed] = null;
                }

                continue;
            }

            bool? prefix = null;
            var value = raw;
            if (value is Context context)
            {
                prefix = context.Prefix;
                value = context.Expression is { } expression
                    ? new DynamicContext(
                        (expressions ?? throw new InvalidOperationException($"The block '{key}' is an expression, but no agent evaluates it.")).Bind(expression))
                    : context.ToDynamicContext() ?? context.Value;
            }

            var content = value is DynamicContext dynamic ? Resolve(key, dynamic, format, logger) : FormatValue(value, format);
            bool isStatic;
            if (prefix is { } explicitPrefix)
            {
                isStatic = explicitPrefix;
            }
            else if (index.TryGetValue(key, out var existing))
            {
                isStatic = blocks[existing].Metadata.Static;
            }
            else if (staticKeys?.Contains(key) == true)
            {
                isStatic = true;
            }
            else
            {
                isStatic = value is not DynamicContext;
            }

            var block = new ResolvedBlock(key, content, Role.System, new BlockMetadata(Static: isStatic));
            if (index.TryGetValue(key, out var position))
            {
                replacements[position] = block;
            }
            else
            {
                appends.Add(block);
            }
        }

        var result = new List<ResolvedBlock>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (!replacements.TryGetValue(i, out var replacement))
            {
                result.Add(blocks[i]);
            }
            else if (replacement is not null)
            {
                result.Add(replacement);
            }
        }

        result.AddRange(appends);
        return result;
    }

    /// <summary>Puts the listed keys first, in the given order. The other blocks follow in their own order.</summary>
    private static List<ResolvedBlock> Reorder(List<ResolvedBlock> blocks, IReadOnlyList<string>? order)
    {
        if (order is null)
        {
            return blocks;
        }

        var rank = new Dictionary<string, int>();
        for (var i = 0; i < order.Count; i++)
        {
            rank.TryAdd(order[i], i);
        }

        return
        [
            .. blocks.Where(b => rank.ContainsKey(b.Key)).OrderBy(b => rank[b.Key]),
            .. blocks.Where(b => !rank.ContainsKey(b.Key)),
        ];
    }

    /// <summary>
    /// Turns the active events into blocks. Events that an archive replaced are not active. An empty response stays in
    /// the log for diagnostics and is not sent: an empty assistant message is invalid for a provider.
    /// </summary>
    private static IEnumerable<ResolvedBlock> EventBlocks(EventManager eventManager, EventQuery? query, string? currentCallId)
    {
        var events = eventManager.Values();
        if (query is not null)
        {
            events = query.Apply(events, currentCallId);
        }

        foreach (var evt in events.Where(e => !e.IsEmpty))
        {
            var tag = evt.Tag ?? evt.Id;
            yield return new ResolvedBlock($"event_{tag}", "", evt.EventRole, new BlockMetadata(Tag: tag), evt);
        }
    }
}
