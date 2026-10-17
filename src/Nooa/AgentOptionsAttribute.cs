// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Static agent settings. On a class, the generator records them in <see cref="AgentMetadata"/>. On an agentic method,
/// they override the class settings for that method.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentOptionsAttribute : Attribute
{
    /// <summary>Model alias or model string for this agent class or method.</summary>
    public string? Llm { get; init; }
}
