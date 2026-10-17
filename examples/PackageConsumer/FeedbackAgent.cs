// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa;
using Nooa.Strategies;

namespace PackageConsumer;

/// <summary>You are an agent specializing in analyzing customer feedback.</summary>
public partial class FeedbackAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Analyze customer feedback. Give the sentiment and a one-sentence summary.</summary>
    [Agentic]
    [Strategy(typeof(PredictStrategy))]
    public partial Task<FeedbackResult> AnalyzeAsync(string text);
}

public sealed record FeedbackResult(string Sentiment, string Summary);
