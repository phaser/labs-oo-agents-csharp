// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class AfterTurnTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new AfterTurn { MethodName = "m", Strategy = "s", GenerationId = "g", TurnNumber = 3, IsFinal = true, Success = false, ExceptionType = "X" });

        Assert.Equal("AfterTurn", back.EventType);
        Assert.Equal(Role.RuntimeEvent, back.EventRole);
        Assert.True(back.IsFinal); Assert.False(back.Success); Assert.Equal("X", back.ExceptionType); Assert.Null(back.ParentGenerationId);
    }
}
