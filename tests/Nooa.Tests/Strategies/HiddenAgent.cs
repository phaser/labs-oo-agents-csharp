// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Strategies;

/// <summary>An agent type that is not public. CodeAct cannot expose it to cell code.</summary>
internal sealed partial class HiddenAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Runs.</summary>
    [Agentic]
    public partial Task<int> RunAsync();
}
