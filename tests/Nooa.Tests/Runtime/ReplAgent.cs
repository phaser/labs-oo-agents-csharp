// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

/// <summary>An agent whose members a REPL cell calls through <c>self</c>.</summary>
public partial class ReplAgent(AgentOptions? options = null) : Agent(options)
{
    public int Counter { get; set; }

    public string Greet(string name) => "hello " + name;
}
