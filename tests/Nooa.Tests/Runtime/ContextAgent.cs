// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

public partial class ContextAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Asks the model and shows the context.</summary>
    [Agentic]
    [Strategy(typeof(BuildingStrategy))]
    public partial Task<string> AskAsync(string question);
}
