// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test hooks that log each call, and can throw.</summary>
public sealed class RecordingHooks : IInstrumentationHooks
{
    public List<string> Log { get; } = [];

    public bool Throw { get; init; }

    public object? BeforeAgentCall(Agent agent, string methodName, IReadOnlyList<CallArgument> arguments, string callId, string? parentCallId)
    {
        Log.Add("before:" + methodName);
        return Throw ? throw new InvalidOperationException("hook") : null;
    }

    public void AfterAgentCall(object? context, Agent agent, string methodName, object? result, Exception? exception)
    {
        Log.Add("after:" + methodName + ":" + result);
        if (Throw)
        {
            throw new InvalidOperationException("hook");
        }
    }

    public object? BeforeGeneration(Agent agent, string methodName, string strategy, string generationId, string? parentGenerationId, string? agentCallId)
    {
        Log.Add("beforeGen:" + strategy);
        return null;
    }

    public void AfterGeneration(object? context, Agent agent, string methodName, object? result, Exception? exception, string generationId) =>
        Log.Add("afterGen");
}
