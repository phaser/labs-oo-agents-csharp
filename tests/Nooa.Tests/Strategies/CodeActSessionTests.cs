// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_codeact_strategy.py (CodeActSession)

using Nooa.Errors;
using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class CodeActSessionTests
{
    [Fact]
    public void IsExhausted_Fresh_IsFalse()
    {
        Assert.False(new CodeActSession(null, 3, "M").IsExhausted);
    }

    [Fact]
    public void IsExhausted_IterationsReachTheLimit_IsTrue()
    {
        var session = new CodeActSession(2, 3, "M");
        session.RecordIteration();
        Assert.False(session.IsExhausted);
        session.RecordIteration();

        Assert.True(session.IsExhausted);
    }

    [Fact]
    public void IsExhausted_ErrorsReachTheRetryLimit_IsTrue()
    {
        var session = new CodeActSession(null, 2, "M");
        session.RecordError();
        session.RecordError();

        Assert.True(session.IsExhausted);
    }

    [Fact]
    public void ConsecutiveTextOnly_CountsAndResets()
    {
        var session = new CodeActSession(null, 3, "M");
        session.RecordTextOnly();
        session.RecordTextOnly();
        Assert.Equal(2, session.ConsecutiveTextOnly);

        session.ResetTextOnly();

        Assert.Equal(0, session.ConsecutiveTextOnly);
    }

    [Fact]
    public void BuildFailure_ErrorsRanOut_NamesTheRetryLimit()
    {
        var session = new CodeActSession(null, 1, "Run");
        session.RecordError();

        var failure = session.BuildFailure();

        Assert.IsType<GenerationException>(failure);
        Assert.Equal("Generation failed after 1 errors (max_retries=1). Unable to generate valid code for `Run`.", failure.Message);
    }

    [Fact]
    public void BuildFailure_IterationsRanOut_NamesTheIterationLimit()
    {
        var session = new CodeActSession(1, 3, "Run");
        session.RecordIteration();

        Assert.Equal("Generation failed after 1 iterations (max_iterations=1). Unable to complete `Run`.", session.BuildFailure().Message);
    }
}
