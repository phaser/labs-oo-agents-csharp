// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Tracing;

/// <summary>Test strategy that plays the Python orchestrator of quickstart 06: it calls the other agentic methods in turn.</summary>
public sealed class MathRunStrategy : GenerationStrategy
{
    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        var agent = (MathAgent)runtime.Agent;
        var expression = (string)call.Arguments[0].Value!;
        var value = await agent.CalculateAsync(expression);
        return await agent.ExplainAsync(expression, MathAgent.Format(value));
    }
}
