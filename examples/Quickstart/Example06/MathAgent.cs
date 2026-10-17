// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/06_tracing.py

namespace Nooa.Examples.Quickstart.Example06;

/// <summary>You are a careful math assistant.</summary>
public partial class MathAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Orchestrator: evaluates the expression, then explains it.</summary>
    [Agentic]
    [Strategy(typeof(MathRunStrategy))]
    public partial Task<string> RunAsync(string expression);

    /// <summary>Evaluates the mathematical expression and returns the numeric result.</summary>
    [Agentic]
    public partial Task<double> CalculateAsync(string expression);

    /// <summary>Explains in one sentence why the expression produces the result.</summary>
    [Agentic]
    public partial Task<string> ExplainAsync(string expression, string result);

    /// <summary>Private helper that formats the result for display.</summary>
    internal static string Format(double value) => value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
}
