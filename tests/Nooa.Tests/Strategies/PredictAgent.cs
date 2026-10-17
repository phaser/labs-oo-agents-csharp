// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Strategies;

/// <summary>You are a prediction test agent.</summary>
public partial class PredictAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Scores the text.</summary>
    [Agentic]
    public partial Task<int> ScoreAsync(string text);

    /// <summary>Lists the words.</summary>
    [Agentic]
    public partial Task<List<string>> ListAsync(string text);

    /// <summary>Scores the text, or nothing.</summary>
    [Agentic]
    public partial Task<int?> MaybeAsync(string text);

    /// <summary>Describes the person in the text.</summary>
    [Agentic]
    public partial Task<PredictPerson> ProfileAsync(string text);

    /// <summary>Has no result.</summary>
    [Agentic]
    public partial Task NoResultAsync(string text);

    /// <summary>Counts the items.</summary>
    [Agentic]
    public partial Task<int> CountAsync(List<int> items);
}
