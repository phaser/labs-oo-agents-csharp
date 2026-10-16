// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class BeforeAgentCallTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new BeforeAgentCall { MethodName = "m", CallId = "c", IsTopLevel = true, NeedsGeneration = true });

        Assert.Equal("BeforeAgentCall", back.EventType);
        Assert.Equal(Role.RuntimeEvent, back.EventRole);
        Assert.Equal(("m", "c", true, true), (back.MethodName, back.CallId, back.IsTopLevel, back.NeedsGeneration)); Assert.Null(back.ParentCallId);
    }
}
