// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class AfterAgentCallTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new AfterAgentCall { MethodName = "m", CallId = "c", ParentCallId = "p", IsTopLevel = false, Success = false, ExceptionType = "X" });

        Assert.Equal("AfterAgentCall", back.EventType);
        Assert.Equal(Role.RuntimeEvent, back.EventRole);
        Assert.Equal(("p", false, false, "X"), (back.ParentCallId, back.IsTopLevel, back.Success, back.ExceptionType));
    }
}
