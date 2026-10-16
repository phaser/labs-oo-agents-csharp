// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context_manager.py

namespace Nooa.Runtime;

/// <summary>
/// One stored context block. A block holds a plain <see cref="Value"/> or a <see cref="Provider"/> that the runtime
/// calls each model turn. Upstream stores a Python expression string. C# stores a delegate (decision D5).
/// </summary>
/// <param name="Value">The plain value. Null when the block has a provider.</param>
/// <param name="Provider">Computes the value each turn. Null for a plain value.</param>
/// <param name="IsStatic">True for the cacheable prefix partition, false for the volatile suffix partition.</param>
public sealed record ContextBlockEntry(object? Value, Func<object?>? Provider, bool IsStatic)
{
    /// <summary>True when the block value is computed each turn.</summary>
    public bool IsDynamic => Provider is not null;
}
