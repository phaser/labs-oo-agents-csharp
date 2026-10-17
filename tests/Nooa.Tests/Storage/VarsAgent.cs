// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CA1051 // Fixture fields are public on purpose.

using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class VarsAgent(AgentOptions? options = null) : Agent(options)
{
    public SnapshotVars Memory = new();
}
