// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Static class-level agent settings. The generator records them in <see cref="AgentMetadata"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AgentOptionsAttribute : Attribute
{
    /// <summary>Model alias or model string for this agent class.</summary>
    public string? Llm { get; init; }
}
