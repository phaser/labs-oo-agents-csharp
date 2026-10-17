// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

/// <summary>An agent with a member that ignores the cancellation token and an LLM method.</summary>
public partial class SpinAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Lets a test stop the loops of <see cref="Spin"/>. Only these tests use it.</summary>
    public static volatile bool Stop;

    /// <summary>Loops until the test sets <see cref="Stop"/>.</summary>
    public string Spin()
    {
        while (!Stop)
        {
        }

        return "done";
    }

    /// <summary>Asks the model.</summary>
    [Agentic]
    public partial Task<string> AskAsync(string question);

    /// <summary>Gives the context API to the model.</summary>
    public new Nooa.Runtime.ContextApi Context => base.Context;
}
