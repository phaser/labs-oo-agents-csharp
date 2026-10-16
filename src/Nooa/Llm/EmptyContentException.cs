// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/errors.py

using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>A model returned reasoning without the requested final content.</summary>
public sealed class EmptyContentException : NooaException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="reasoning">The reasoning that the model returned, if any.</param>
    public EmptyContentException(string? reasoning = null)
        : base(reasoning is { Length: > 0 }
            ? $"Empty content with reasoning: {(reasoning.Length > 100 ? reasoning[..100] : reasoning)}..."
            : "Empty content")
    {
        Reasoning = reasoning;
    }

    /// <summary>The reasoning that the model returned, if any.</summary>
    public string? Reasoning { get; }
}
