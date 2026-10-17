// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_strategy_validators.py

using Nooa.Errors;
using Nooa.Llm;
using Nooa.Strategies;
using Nooa.Tests.Runtime;

namespace Nooa.Tests.Strategies;

public class StrategyConditionsTests
{
    private static readonly CurrentCall _call = new() { MethodName = "m" };

    private static Agent NewAgent() => new PlainAgent(new AgentOptions { Llm = new FakeLlmClient() });

    private static void BuggyPost(Agent agent, object? result, CurrentCall call) => throw new KeyNotFoundException("boom");

    private static void BuggyPre(Agent agent, CurrentCall call) => throw new InvalidOperationException("boom");

    [Fact]
    public void RunPostconditions_InvariantFailure_IsRethrown()
    {
        void Rejects(Agent agent, object? result, CurrentCall call) => throw new InvariantException("bad");

        var error = Assert.Throws<InvariantException>(() => StrategyConditions.RunPostconditions(NewAgent(), 1, _call, [Rejects]));

        Assert.Equal("bad", error.Message);
    }

    [Fact]
    public void RunPostconditions_OtherFailure_IsWrappedAsRuntimeError()
    {
        var error = Assert.Throws<NooaRuntimeException>(() => StrategyConditions.RunPostconditions(NewAgent(), 1, _call, [BuggyPost]));

        Assert.Equal("method postcondition 'BuggyPost' failed", error.Message);
        Assert.IsType<KeyNotFoundException>(error.InnerException);
    }

    [Fact]
    public void RunPostconditions_Cancellation_IsNotWrapped()
    {
        static void Cancels(Agent agent, object? result, CurrentCall call) => throw new OperationCanceledException();

        Assert.Throws<OperationCanceledException>(() => StrategyConditions.RunPostconditions(NewAgent(), 1, _call, [Cancels]));
    }

    [Fact]
    public void RunPostconditions_PassesAgentResultAndCall()
    {
        var agent = NewAgent();
        (Agent, object?, CurrentCall)? seen = null;

        StrategyConditions.RunPostconditions(agent, "r", _call, [(a, r, c) => seen = (a, r, c)]);

        Assert.Same(agent, seen!.Value.Item1);
        Assert.Equal("r", seen.Value.Item2);
        Assert.Same(_call, seen.Value.Item3);
    }

    [Fact]
    public void RunPreconditions_PassesAgentAndCall()
    {
        var agent = NewAgent();
        (Agent, CurrentCall)? seen = null;

        StrategyConditions.RunPreconditions(agent, _call, [(a, c) => seen = (a, c)]);

        Assert.Same(agent, seen!.Value.Item1);
        Assert.Same(_call, seen.Value.Item2);
    }

    [Fact]
    public void RunPreconditions_FailureStopsTheRun()
    {
        var ran = false;
        void Rejects(Agent agent, CurrentCall call) => throw new InvariantException("no");

        Assert.Throws<InvariantException>(() => StrategyConditions.RunPreconditions(NewAgent(), _call, [Rejects, (a, c) => ran = true]));
        Assert.False(ran);
    }

    [Fact]
    public void RunPreconditions_OtherFailure_IsWrappedAsRuntimeError()
    {
        var error = Assert.Throws<NooaRuntimeException>(() => StrategyConditions.RunPreconditions(NewAgent(), _call, [BuggyPre]));

        Assert.Equal("method precondition 'BuggyPre' failed", error.Message);
    }

    [Fact]
    public void Run_NoConditions_DoesNothing()
    {
        StrategyConditions.RunPreconditions(NewAgent(), _call, []);
        StrategyConditions.RunPostconditions(NewAgent(), null, _call, []);
    }
}
