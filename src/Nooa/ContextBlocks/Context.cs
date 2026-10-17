// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/models.py

namespace Nooa.ContextBlocks;

/// <summary>
/// A context block value that controls content and placement. Content is a literal value, a provider that the
/// runtime calls each turn, or a C# expression that the runtime evaluates each turn (<see cref="Expr"/>).
/// Placement is the cacheable prefix (<see cref="Prefix"/> true) or the volatile suffix.
/// </summary>
/// <remarks>
/// An expression is code. The agent evaluates it in a C# session with <c>self</c> bound to the agent, after the same
/// checks as a code cell of the model. A context manager that has no agent refuses an expression block.
/// </remarks>
public sealed record Context
{
    /// <summary>The longest expression text in characters.</summary>
    public const int MaxExpressionLength = 4096;

    private Context(object? value, Func<object?>? provider, bool prefix, string? expression = null)
    {
        Value = value;
        Provider = provider;
        Prefix = prefix;
        Expression = expression;
    }

    /// <summary>The literal value. Null when the block has a provider.</summary>
    public object? Value { get; }

    /// <summary>Computes the value each turn. Null for a literal block.</summary>
    public Func<object?>? Provider { get; }

    /// <summary>True for the cacheable prefix partition, false for the volatile suffix partition.</summary>
    public bool Prefix { get; }

    /// <summary>The C# expression that is evaluated each turn. Null for a literal block or a provider block.</summary>
    public string? Expression { get; }

    /// <summary>True when the value is computed each turn.</summary>
    public bool IsDynamic => Provider is not null || Expression is not null;

    /// <summary>Creates a literal block.</summary>
    public static Context Of(object value, bool prefix = false) =>
        new(value ?? throw new ArgumentNullException(nameof(value)), null, prefix);

    /// <summary>Creates a block that the runtime computes each turn.</summary>
    public static Context FromProvider(Func<object?> provider, bool prefix = false) =>
        new(null, provider ?? throw new ArgumentNullException(nameof(provider)), prefix);

    /// <summary>
    /// Creates a block that the agent evaluates as a C# expression each turn, for example <c>self.RenderNotes()</c>.
    /// It must be a single expression, not a statement.
    /// </summary>
    /// <exception cref="ArgumentException">The expression is empty or longer than <see cref="MaxExpressionLength"/>.</exception>
    public static Context Expr(string expression, bool prefix = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (expression.Length > MaxExpressionLength)
        {
            throw new ArgumentException($"The expression is longer than {MaxExpressionLength} characters.", nameof(expression));
        }

        return new(null, null, prefix, expression);
    }

    /// <summary>Returns the dynamic form of a provider block, or null for a literal block.</summary>
    public DynamicContext? ToDynamicContext() => Provider is null ? null : new DynamicContext(Provider);
}
