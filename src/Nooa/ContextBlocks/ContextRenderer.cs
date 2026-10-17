// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/renderer.py

using System.Globalization;
using Microsoft.Extensions.Logging;
using Nooa.Config;

namespace Nooa.ContextBlocks;

/// <summary>
/// Renders resolved blocks into the messages of a model request, with utilization stats.
/// </summary>
/// <remarks>
/// <para>The steps are:</para>
/// <list type="number">
/// <item>Serialize the content of the event blocks so that it can be measured. A tool call event stays structural.</item>
/// <item>Apply the total token budget: system blocks over budget become <c>EVICTED</c> in place.</item>
/// <item>Hand all blocks to the <see cref="BlockFormatter"/>, which builds the messages.</item>
/// </list>
/// <para>The input blocks are never changed. Block content passes through unchanged: there is no per-block cut.</para>
/// </remarks>
public static class ContextRenderer
{
    /// <summary>Renders the blocks.</summary>
    /// <param name="blocks">The resolved blocks.</param>
    /// <param name="formatter">Builds the messages.</param>
    /// <param name="contextLimit">The token budget for the system blocks, or null for none.</param>
    /// <param name="countTokens">Counts the tokens of a text. It is required with <paramref name="contextLimit"/>.</param>
    /// <param name="eventFormat">The default bounds for the fields of an event.</param>
    /// <param name="eventFormatResolver">
    /// Picks the bounds for one event. It can differ from the default, so one method can set its own bounds
    /// for its events without a change to the rest of the context. A null result uses <paramref name="eventFormat"/>.
    /// </param>
    /// <param name="modelContextWindow">The model window, for the stats.</param>
    /// <param name="reservedOutputTokens">The tokens kept for the reply, for the stats.</param>
    /// <param name="logger">Receives a warning when a turn is left out.</param>
    /// <exception cref="ArgumentException"><paramref name="contextLimit"/> is set and <paramref name="countTokens"/> is not.</exception>
    public static RenderResult Render(
        IReadOnlyList<ResolvedBlock> blocks,
        BlockFormatter formatter,
        int? contextLimit = null,
        Func<string, int>? countTokens = null,
        FormatConfig? eventFormat = null,
        Func<EventBase, FormatConfig?>? eventFormatResolver = null,
        int? modelContextWindow = null,
        int? reservedOutputTokens = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(formatter);
        if (countTokens is null && contextLimit is not null)
        {
            throw new ArgumentException("A context limit needs a token counter. Pass countTokens.", nameof(countTokens));
        }

        var count = countTokens ?? (text => text.Length);
        var systemBlocks = blocks.Where(b => b.Role == Role.System).ToList();
        var messageBlocks = blocks
            .Where(b => b.Role != Role.System)
            .Select(b => b.Event is { } evt and not ToolCallEvent
                ? b with { Content = formatter.FormatEvent(evt, eventFormatResolver?.Invoke(evt) ?? eventFormat) }
                : b)
            .ToList();

        var dropped = 0;
        if (contextLimit is { } limit)
        {
            (systemBlocks, dropped) = ApplyContextTotalLimit(systemBlocks, limit, count);
        }

        var stats = new ContextWindowStats
        {
            ContextBlocksCount = systemBlocks.Count,
            EventsCount = messageBlocks.Count,
            ContextBlocksChars = systemBlocks.Sum(b => b.Content.Length),
            EventsChars = messageBlocks.Sum(b => b.Content.Length),
            MaxContextTokens = contextLimit,
            ModelContextWindow = modelContextWindow,
            ContextBlocksDropped = dropped,
            ReservedOutputTokens = reservedOutputTokens,
        };
        return new RenderResult(formatter.Format([.. systemBlocks, .. messageBlocks], logger), stats);
    }

    /// <summary>
    /// Marks over-budget blocks as EVICTED. User blocks (not static) go first, from the end. If the total still does not
    /// fit, any other block that is not static goes, from the end. An evicted block keeps its key and position and
    /// shows its size.
    /// </summary>
    private static (List<ResolvedBlock> Blocks, int Evicted) ApplyContextTotalLimit(
        List<ResolvedBlock> blocks, int totalLimit, Func<string, int> count)
    {
        var total = blocks.Sum(b => count(b.Content));
        if (total <= totalLimit)
        {
            return (blocks, 0);
        }

        var sizes = new Dictionary<int, int>();
        for (var i = blocks.Count - 1; i >= 0 && total > totalLimit; i--)
        {
            if (blocks[i].Metadata.UserBlock && !blocks[i].Metadata.Static)
            {
                var size = count(blocks[i].Content);
                total -= size;
                sizes[i] = size;
            }
        }

        for (var i = blocks.Count - 1; i >= 0 && total > totalLimit; i--)
        {
            if (!sizes.ContainsKey(i) && !blocks[i].Metadata.Static)
            {
                var size = count(blocks[i].Content);
                total -= size;
                sizes[i] = size;
            }
        }

        var result = blocks
            .Select((block, i) => sizes.TryGetValue(i, out var size)
                ? block with
                {
                    Content = $"EVICTED: over context budget (block_tokens={size.ToString("N0", CultureInfo.InvariantCulture)})",
                    Metadata = block.Metadata with { Truncated = true },
                }
                : block)
            .ToList();
        return (result, sizes.Count);
    }
}
