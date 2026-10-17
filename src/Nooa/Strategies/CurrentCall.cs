// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/current_call.py

using Nooa.Agentdoc;
using Nooa.Config;

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

    /// <summary>
    /// Formats the arguments as one <c>name = value</c> line each, for a prompt. Returns an empty string
    /// when there are no arguments.
    /// </summary>
    /// <param name="valueFormatter">Formats each value. It takes precedence over <paramref name="truncation"/>.</param>
    /// <param name="truncation">
    /// When set (and there is no <paramref name="valueFormatter"/>), values use its <see cref="TruncationConfig.PrefillFormat"/> limits.
    /// With neither, values show in full (<see cref="PFormat"/> without limits, the analog of Python <c>repr</c>).
    /// </param>
    public string FormatParametersAsCode(Func<object?, string>? valueFormatter = null, TruncationConfig? truncation = null)
    {
        var format = valueFormatter
            ?? (truncation is null
                ? value => PFormat.Format(value)
                : value => PFormat.Format(value, truncation.PrefillFormat));
        return string.Join('\n', Arguments.Select(a => $"{a.Name} = {format(a.Value)}"));
    }
}
