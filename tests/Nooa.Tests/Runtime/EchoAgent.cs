// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

/// <summary>You are a test agent.</summary>
public partial class EchoAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>A counter that snapshots keep.</summary>
    public int Counter;

    /// <summary>Notes that snapshots keep.</summary>
    public List<string> Notes { get; set; } = [];

    /// <summary>Scratch data that snapshots skip.</summary>
    [NoSnapshot]
    public int Scratch;

    /// <summary>Asks the model.</summary>
    [Agentic]
    [Strategy(typeof(EchoStrategy))]
    public partial Task<string> AskAsync(string question, CancellationToken cancellationToken = default);

    /// <summary>Uses the default strategy.</summary>
    [Agentic]
    public partial Task<string> DefaultAsync(string question);

    /// <summary>Uses a model alias set on the method.</summary>
    [Agentic]
    [Strategy(typeof(EchoStrategy))]
    [AgentOptions(Llm = "method-model")]
    public partial Task<string> MethodModelAsync(string question);

    /// <summary>Calls another agentic method.</summary>
    [Agentic]
    [Strategy(typeof(NestingStrategy))]
    public partial Task<string> OuterAsync(string question);

    /// <summary>Runs with the gate strategy.</summary>
    [Agentic]
    [Strategy(typeof(GateStrategy))]
    public partial Task<string> GatedAsync(string question, CancellationToken cancellationToken = default);

    /// <summary>Always fails.</summary>
    [Agentic]
    [Strategy(typeof(FailingStrategy))]
    public partial Task<string> FailAsync(string question);

    /// <summary>Runs without a trace.</summary>
    [Agentic]
    [NoTrace]
    [Strategy(typeof(EchoStrategy))]
    public partial Task<string> QuietAsync(string question);

    /// <summary>Has no result.</summary>
    [Agentic]
    [Strategy(typeof(EchoStrategy))]
    public partial Task VoidAsync(string question);

    /// <summary>Returns a number.</summary>
    [Agentic]
    [Strategy(typeof(EchoStrategy))]
    public partial Task<int> NumberAsync(string question);

    /// <summary>Builds a child agent inside the call.</summary>
    [Agentic]
    [Strategy(typeof(ChildBuildingStrategy))]
    public partial Task BuildChildAsync();
}
