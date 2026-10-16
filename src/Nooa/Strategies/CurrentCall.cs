// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/current_call.py

namespace Nooa.Strategies;

/// <summary>
/// The data of one agent method call that a strategy generates. The generated body of an
/// <see cref="AgenticAttribute"/> method builds it.
/// </summary>
/// <remarks>
/// Upstream <c>args</c> and <c>kwargs</c> are one ordered <see cref="Arguments"/> list here, so
/// <c>bound_parameters()</c> has no equivalent. A trailing <see cref="CancellationToken"/> is not an argument.
/// </remarks>
public sealed record CurrentCall
{
    /// <summary>Unique call id for correlation and tracing.</summary>
    public string Id { get; init; } = "call_" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Name of the method.</summary>
    public required string MethodName { get; init; }

    /// <summary>The method signature as text, for example <c>(string data) -> int</c>.</summary>
    public string? Signature { get; init; }

    /// <summary>The method prompt: the XML doc comment with placeholders filled in. Null when the method has none.</summary>
    public string? Docstring { get; init; }

    /// <summary>The model-visible arguments in parameter order.</summary>
    public IReadOnlyList<CallArgument> Arguments { get; init; } = [];

    /// <summary>The result type. Null when the method returns <see cref="Task"/> or <see cref="ValueTask"/>.</summary>
    public Type? ReturnType { get; init; }

    /// <summary>The <see cref="StrategyAttribute"/> type of the method, or null.</summary>
    public Type? StrategyType { get; init; }

    /// <summary>Id of the calling call for nested calls. The runtime sets it.</summary>
    public string? ParentId { get; init; }
}
