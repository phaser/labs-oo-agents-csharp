// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tests.Strategies.Acceptance;
using Nooa.Tools;

namespace Nooa.Tests.Strategies;

/// <summary>You are a CodeAct test agent.</summary>
public partial class CodeActAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>The todo list. The context block todo_status shows its status.</summary>
    public TodoManager Todo { get; } = new();

    /// <summary>Doubles a number.</summary>
    public int Twice(int n) => n * 2;

    /// <summary>Answers the question with a number.</summary>
    [Agentic]
    public partial Task<int> AnswerAsync(string question);

    /// <summary>Answers the question with a ratio.</summary>
    [Agentic]
    public partial Task<double> RatioAsync(string question);

    /// <summary>Answers the question with a sentence.</summary>
    [Agentic]
    public partial Task<string> SayAsync(string question);

    /// <summary>Builds an order result.</summary>
    [Agentic]
    public partial Task<OrderResult> BuildAsync(int n);

    /// <summary>Analyzes the text. The result type has required members.</summary>
    [Agentic]
    public partial Task<FeedbackAnalysis> AnalyzeAsync(string text);

    /// <summary>Lists words.</summary>
    [Agentic]
    public partial Task<List<string>> WordsAsync();

    /// <summary>Does the work and returns nothing.</summary>
    [Agentic]
    public partial Task WorkAsync();

    /// <summary>Waits for the work.</summary>
    [Agentic]
    public partial Task<int> WaitForAsync(Task<int> work);

    /// <summary>Answers with a number and accepts a cancellation token.</summary>
    [Agentic]
    public partial Task<int> SlowAsync(string question, CancellationToken cancellationToken = default);
}
