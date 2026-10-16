// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

/// <summary>Has a class-level model alias.</summary>
[AgentOptions(Llm = "class-model")]
public partial class ClassAliasAgent(AgentOptions? options = null) : Agent(options)
{
}
