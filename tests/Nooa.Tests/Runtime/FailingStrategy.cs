// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test strategy: always throws.</summary>
public sealed class FailingStrategy : GenerationStrategy
{
    public override Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("strategy failed");
}
