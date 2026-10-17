// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/02_structured_outputs.py

using Nooa.Strategies;

namespace Nooa.Examples.Quickstart.Example02;

/// <summary>Agent for analyzing customer feedback with structured output.</summary>
public partial class StructuredFeedbackAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Analyze customer feedback comprehensively.</summary>
    [Agentic]
    [Strategy(typeof(PredictStrategy))]
    public partial Task<FeedbackAnalysis> AnalyzeFeedbackAsync(string text);
}
