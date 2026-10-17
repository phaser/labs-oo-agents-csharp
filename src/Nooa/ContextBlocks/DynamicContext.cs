// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/models.py

namespace Nooa.ContextBlocks;

/// <summary>
/// Marks a context block whose value the runtime computes again each turn.
/// Upstream wraps a Python expression. C# wraps a delegate (decision D5).
/// </summary>
/// <param name="Provider">Computes the value each turn.</param>
public sealed record DynamicContext(Func<object?> Provider)
{
    /// <summary>The delegate that computes the value each turn.</summary>
    public Func<object?> Provider { get; } = Provider ?? throw new ArgumentNullException(nameof(Provider));
}
