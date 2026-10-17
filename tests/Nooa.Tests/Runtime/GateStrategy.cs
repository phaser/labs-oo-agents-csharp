// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test strategy: measures how many calls run at the same time.</summary>
public sealed class GateStrategy(bool requiresLock) : GenerationStrategy
{
    private int _running;

    public override bool RequiresLock => requiresLock;

    public int MaxConcurrent { get; private set; }

    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        var now = Interlocked.Increment(ref _running);
        MaxConcurrent = Math.Max(MaxConcurrent, now);
        await Task.Delay(30, cancellationToken);
        Interlocked.Decrement(ref _running);
        return "done";
    }
}
