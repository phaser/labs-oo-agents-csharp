// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/token_counter.py

namespace Nooa.Llm;

/// <summary>Token-counting utilities for context and event token limits.</summary>
public static class TokenCounter
{
    /// <summary>
    /// Approximates the token count as the character length divided by 4 (rounded down). English text
    /// averages about 4 characters per token. Use it for context-window budgets, not for billing.
    /// Characters are Unicode code points, as in upstream.
    /// </summary>
    public static int CharApproximate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.EnumerateRunes().Count() / 4;
    }
}
