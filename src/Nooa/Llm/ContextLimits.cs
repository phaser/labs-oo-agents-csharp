// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/limits.py

using System.Globalization;
using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>
/// The selected model window minus the reply allowance of the effective request.
/// </summary>
/// <remarks>
/// A fallback is a planning reserve, not a claim about the default of the server.
/// An unknown window stays unknown. A reply ceiling is not a requested reply cap.
/// </remarks>
public sealed record ContextLimits
{
    /// <summary>The names of the request settings that cap the reply length, as the providers spell them.</summary>
    public static readonly IReadOnlySet<string> ReplyCapKeys =
        new HashSet<string> { "max_tokens", "max_completion_tokens", "max_output_tokens" };

    /// <summary>Creates the limits.</summary>
    /// <param name="contextWindow">The model window in tokens, or null when unknown.</param>
    /// <param name="reservedOutputTokens">The tokens kept for the reply.</param>
    /// <param name="reserveIsFallback">True when the reserve is a planning value, not a requested cap.</param>
    /// <exception cref="ConfigValidationException">A configured reply cap leaves no room for input.</exception>
    public ContextLimits(int? contextWindow, int reservedOutputTokens, bool reserveIsFallback = false)
    {
        if (!reserveIsFallback && contextWindow is { } window && reservedOutputTokens >= window)
        {
            throw new ConfigValidationException(
                $"Configured reply cap ({reservedOutputTokens.ToString("N0", CultureInfo.InvariantCulture)}) leaves no room for input "
                + $"in the context window ({window.ToString("N0", CultureInfo.InvariantCulture)}). Reduce the reply cap or "
                + "correct the context_window of the model before you use context management.");
        }

        ContextWindow = contextWindow;
        ReservedOutputTokens = reservedOutputTokens;
        ReserveIsFallback = reserveIsFallback;
    }

    /// <summary>The model window in tokens, or null when unknown.</summary>
    public int? ContextWindow { get; }

    /// <summary>The tokens kept for the reply.</summary>
    public int ReservedOutputTokens { get; }

    /// <summary>True when <see cref="ReservedOutputTokens"/> is a planning value, not a requested cap.</summary>
    public bool ReserveIsFallback { get; }

    /// <summary>The remaining input capacity, or null when the window is unknown.</summary>
    public int? UsableInputTokens => ContextWindow is { } window ? Math.Max(1, window - ReservedOutputTokens) : null;

    /// <summary>
    /// Reads the limits of <paramref name="client"/>. A window that is not positive counts as unknown.
    /// </summary>
    /// <param name="client">The client to ask for its window.</param>
    /// <param name="replyCap">The explicit reply cap of the request, or null.</param>
    /// <param name="fallbackReserve">The reserve to plan with when there is no explicit cap.</param>
    public static ContextLimits For(ILlmClient client, int? replyCap = null, int fallbackReserve = 0)
    {
        ArgumentNullException.ThrowIfNull(client);
        var window = client.ContextWindow is > 0 ? client.ContextWindow : null;
        return new ContextLimits(window, replyCap ?? fallbackReserve, reserveIsFallback: replyCap is null);
    }
}
