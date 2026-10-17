// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/models.py

namespace Nooa.ContextBlocks;

/// <summary>
/// A context block value that controls content and placement. Content is a literal value or a provider that the
/// runtime calls each turn. Placement is the cacheable prefix (<see cref="Prefix"/> true) or the volatile suffix.
/// </summary>
/// <remarks>
/// Upstream also has an expression form, <c>Context(expr=...)</c>. It needs the REPL evaluator and arrives with T18.
/// </remarks>
public sealed record Context
{
    private Context(object? value, Func<object?>? provider, bool prefix)
    {
        Value = value;
        Provider = provider;
        Prefix = prefix;
    }

    /// <summary>The literal value. Null when the block has a provider.</summary>
    public object? Value { get; }

    /// <summary>Computes the value each turn. Null for a literal block.</summary>
    public Func<object?>? Provider { get; }

    /// <summary>True for the cacheable prefix partition, false for the volatile suffix partition.</summary>
    public bool Prefix { get; }

    /// <summary>True when the value is computed each turn.</summary>
    public bool IsDynamic => Provider is not null;

    /// <summary>Creates a literal block.</summary>
    public static Context Of(object value, bool prefix = false) =>
        new(value ?? throw new ArgumentNullException(nameof(value)), null, prefix);

    /// <summary>Creates a block that the runtime computes each turn.</summary>
    public static Context FromProvider(Func<object?> provider, bool prefix = false) =>
        new(null, provider ?? throw new ArgumentNullException(nameof(provider)), prefix);

    /// <summary>Returns the dynamic form of a provider block, or null for a literal block.</summary>
    public DynamicContext? ToDynamicContext() => Provider is null ? null : new DynamicContext(Provider);
}
