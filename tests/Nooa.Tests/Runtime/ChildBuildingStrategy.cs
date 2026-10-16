// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test strategy: builds a child agent without an LLM, as generated code would.</summary>
public sealed class ChildBuildingStrategy : GenerationStrategy
{
    public EchoAgent? Built { get; private set; }

    public override Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        Built = new EchoAgent();
        return Task.FromResult<object?>(null);
    }
}
