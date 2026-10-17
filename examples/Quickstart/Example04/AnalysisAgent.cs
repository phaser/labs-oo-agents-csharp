// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/04_strategies.py

using Nooa.Strategies;
using Nooa.Tools;

namespace Nooa.Examples.Quickstart.Example04;

/// <summary>Agent demonstrating different strategy options.</summary>
public partial class AnalysisAgent : Agent
{
    /// <summary>Creates the agent. It owns the shell and closes it with the agent.</summary>
    public AnalysisAgent(AgentOptions? options = null, string cwd = ".")
        : base(options)
    {
        // The shell runs commands as the host user. This agent creates it on purpose and keeps it local.
        Shell = new ShellTools(new ShellOptions { Cwd = cwd });
        EventManager.OnClose(() => Shell.DisposeAsync().AsTask());
    }

    /// <summary>The persistent shell and file tools.</summary>
    public ShellTools Shell { get; }

    /// <summary>Classify as positive, negative, or neutral.</summary>
    [Agentic]
    [Strategy(typeof(PredictStrategy))]
    public partial Task<string> ClassifySentiment(string text);

    /// <summary>Perform the task requested by the user and provide a friendly response.</summary>
    [Agentic]
    [Strategy(typeof(CodeActStrategy))]
    public partial Task<string> PerformTask(string request);
}
