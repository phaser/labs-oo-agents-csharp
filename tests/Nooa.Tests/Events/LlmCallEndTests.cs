// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class LlmCallEndTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new LlmCallEnd { MethodName = "m", Strategy = "s", GenerationId = "g", TurnNumber = 1, Success = false, ExceptionType = "X" });

        Assert.Equal("LLMCallEnd", back.EventType);
        Assert.Equal(Role.RuntimeEvent, back.EventRole);
        Assert.False(back.Success); Assert.Equal("X", back.ExceptionType);
    }

    [Fact]
    public void Defaults_SuccessTrue_NoException()
    {
        var e = new LlmCallEnd { MethodName = "m", Strategy = "s", GenerationId = "g", TurnNumber = 1 };

        Assert.True(e.Success);
        Assert.Null(e.ExceptionType);
    }
}
