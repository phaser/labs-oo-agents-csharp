// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

/// <summary>Names a type that is not a strategy.</summary>
public partial class BadStrategyAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Bad.</summary>
    [Agentic]
    [Strategy(typeof(string))]
    public partial Task<string> BadAsync(string question);
}
