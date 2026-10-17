// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/01_first_generation_method.py

namespace Nooa.Tests.Strategies.Acceptance;

/// <summary>You are an agent specializing in analyzing customer feedback.</summary>
public partial class FeedbackAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Analyze customer feedback for sentiment and key topics in one sentence.</summary>
    [Agentic]
    public partial Task<string> AnalyzeFeedbackAsync(string text);
}
