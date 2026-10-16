// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class LlmCallStartTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new LlmCallStart { MethodName = "m", Strategy = "s", GenerationId = "g", TurnNumber = 1 });

        Assert.Equal("LLMCallStart", back.EventType);
        Assert.Equal(Role.RuntimeEvent, back.EventRole);
        Assert.Equal(("m", "s", "g", 1), (back.MethodName, back.Strategy, back.GenerationId, back.TurnNumber));
    }
}
