// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py

namespace Nooa.Runtime;

/// <summary>The globals of a cell that runs for one agent. <see cref="self"/> has the type of the agent.</summary>
/// <typeparam name="TAgent">The agent type. It must be public so that cell code can name its members.</typeparam>
public sealed class ReplGlobals<TAgent> : ReplGlobals
    where TAgent : class
{
    /// <summary>Creates the globals for an agent.</summary>
    public ReplGlobals(TAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        self = agent;
    }

    /// <summary>The agent. Its public members are the capabilities of the cell.</summary>
    public TAgent self { get; }
}
