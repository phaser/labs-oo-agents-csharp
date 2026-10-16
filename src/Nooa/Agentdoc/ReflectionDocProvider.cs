// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>
/// Fallback provider. It reads <see cref="DescriptionAttribute"/> and returns null for everything else.
/// </summary>
public sealed class ReflectionDocProvider : IAgentDocProvider
{
    /// <summary>The shared instance.</summary>
    public static ReflectionDocProvider Instance { get; } = new();

    /// <inheritdoc />
    public string? GetSummary(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;
    }

    /// <inheritdoc />
    public string? GetParameterSummary(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return parameter.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;
    }
}
