// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/models.py

using System.Globalization;

namespace Nooa.ContextBlocks;

/// <summary>
/// A snapshot of the context window use.
/// </summary>
/// <remarks>
/// <para>
/// The single source of truth for token use is <see cref="PromptTokens"/>: the prompt token count that the provider
/// reported for the latest response. There is no local token estimate. Before the first response the value is null,
/// and all derived token figures are null too.
/// </para>
/// <para>
/// <see cref="ContextRenderer.Render"/> fills only the structural fields (counts and character sizes). The runtime
/// writes <see cref="PromptTokens"/> after the provider returns usage. The split between context blocks and events is
/// attributed from the total by character share, so it is approximate and always adds up to <see cref="PromptTokens"/>.
/// </para>
/// </remarks>
public sealed record ContextWindowStats
{
    /// <summary>Number of system blocks, after eviction.</summary>
    public required int ContextBlocksCount { get; init; }

    /// <summary>Number of event blocks.</summary>
    public required int EventsCount { get; init; }

    /// <summary>Provider-reported prompt tokens from the latest response. Null until the first response returns usage.</summary>
    public int? PromptTokens { get; init; }

    /// <summary>Raw character size of the system blocks, for the breakdown.</summary>
    public int ContextBlocksChars { get; init; }

    /// <summary>Raw character size of the event blocks, for the breakdown.</summary>
    public int EventsChars { get; init; }

    /// <summary>The configured eviction budget for context blocks. For information only.</summary>
    public int? MaxContextTokens { get; init; }

    /// <summary>The total context window of the model. It is the denominator of the utilization.</summary>
    public int? ModelContextWindow { get; init; }

    /// <summary>System blocks that eviction marked EVICTED.</summary>
    public int ContextBlocksDropped { get; init; }

    /// <summary>Events that truncation dropped.</summary>
    public int EventsDropped { get; init; }

    /// <summary>
    /// Tokens kept for the reply of the next call: the call cap when known, otherwise the configured reserve.
    /// A prompt fits only if the prompt plus the output is within the window.
    /// </summary>
    public int? ReservedOutputTokens { get; init; }

    /// <summary>True when the reserve is a planning allowance and not a configured reply cap.</summary>
    public bool OutputReserveIsFallback { get; init; }

    /// <summary>Provider-reported prompt tokens, or null before the first response.</summary>
    public int? TotalTokens => PromptTokens;

    /// <summary>The provider total attributed to context blocks by character share. Null until <see cref="PromptTokens"/> is known.</summary>
    public int? ContextBlocksTokens
    {
        get
        {
            if (PromptTokens is not { } prompt)
            {
                return null;
            }

            var totalChars = ContextBlocksChars + EventsChars;
            return totalChars <= 0 ? 0 : (int)Math.Round((double)prompt * ContextBlocksChars / totalChars, MidpointRounding.ToEven);
        }
    }

    /// <summary>
    /// The provider total attributed to events. It is the remainder after the context blocks, so the two add up to
    /// <see cref="PromptTokens"/>. Null until <see cref="PromptTokens"/> is known.
    /// </summary>
    public int? EventsTokens => PromptTokens is { } prompt ? prompt - (ContextBlocksTokens ?? 0) : null;

    /// <summary>The usable input window: the model window minus the output reserve. Null when the window is unknown.</summary>
    public int? EffectiveWindow =>
        ModelContextWindow is > 0 and var window ? Math.Max(1, window - (ReservedOutputTokens ?? 0)) : null;

    /// <summary>
    /// The fraction of the usable window that the prompt uses, or null when it is unknown. It can exceed 1.0 when the
    /// prompt has grown into the output reserve.
    /// </summary>
    public double? OverallUtilization =>
        PromptTokens is { } prompt && EffectiveWindow is > 0 and var window ? (double)prompt / window : null;

    /// <summary>Returns a human-readable summary that is fit for a context block.</summary>
    public string Format()
    {
        const string guidance =
            "Compact history: self.Events.Collapse(startTag, endTag, summaryText: ...); see Doc(self.Events).\n"
            + "Add, remove, or edit context blocks: Doc(self.Context).";
        if (PromptTokens is not { } prompt)
        {
            return "Context: awaiting first model response\n" + guidance;
        }

        var lines = new List<string>();
        if (ModelContextWindow is { } window && EffectiveWindow is { } usable && window > 0)
        {
            var pct = (double)prompt / usable * 100;
            var reserve = ReservedOutputTokens ?? 0;
            if (reserve != 0)
            {
                var label = OutputReserveIsFallback ? "planning reserve" : "output reserve";
                lines.Add($"Context: {Number(prompt)} / {Number(usable)} usable tokens ({Percent(pct)}%) · {label}: {Number(reserve)}");
            }
            else
            {
                lines.Add($"Context: {Number(prompt)} / {Number(window)} tokens ({Percent(pct)}%)");
            }
        }
        else
        {
            lines.Add($"Context: {Number(prompt)} tokens");
        }

        var blockParts = new List<string> { $"~{Number(ContextBlocksTokens ?? 0)} tokens", $"{ContextBlocksCount} blocks" };
        if (ContextBlocksDropped != 0)
        {
            blockParts.Add($"{ContextBlocksDropped} EVICTED");
        }

        lines.Add($"  Context blocks: {string.Join(" — ", blockParts)}");

        var eventParts = new List<string> { $"~{Number(EventsTokens ?? 0)} tokens", $"{EventsCount} events" };
        if (EventsDropped != 0)
        {
            eventParts.Add($"{EventsDropped} dropped");
        }

        lines.Add($"  Events:         {string.Join(" — ", eventParts)}");

        var hot = OverallUtilization is > 0.8;
        if (ContextBlocksDropped != 0 || EventsDropped != 0 || hot)
        {
            lines.Add("Context is nearly full. Context blocks over budget are labeled EVICTED.");
        }

        lines.Add(guidance);
        return string.Join("\n", lines);
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Percent(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
