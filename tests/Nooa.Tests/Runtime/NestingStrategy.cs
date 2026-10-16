// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test strategy: calls another agentic method of the agent from inside the generation session.</summary>
public sealed class NestingStrategy : GenerationStrategy
{
    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        var agent = (EchoAgent)runtime.Agent;
        var inner = await agent.AskAsync("inner", cancellationToken);
        return "outer:" + inner;
    }
}
