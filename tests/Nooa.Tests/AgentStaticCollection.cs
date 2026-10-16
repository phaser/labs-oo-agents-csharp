// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

/// <summary>Serializes tests that change process-wide agent state: <c>Agent.DefaultLlmResolver</c> and <c>InstrumentationHooks.Current</c>.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AgentStaticCollection
{
    public const string Name = "Agent static state";
}
