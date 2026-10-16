// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class LoopGuardTriggeredTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new LoopGuardTriggered { Content = "stop", Action = "blocked", ToolName = "run", ToolCallId = "c1", Repeats = 7, Window = 15, Fingerprint = "abc" });

        Assert.Equal("LoopGuardTriggered", back.EventType);
        Assert.Equal(Role.User, back.EventRole);
        Assert.Equal(("blocked", "run", "c1", 7, 15, "abc"), (back.Action, back.ToolName, back.ToolCallId, back.Repeats, back.Window, back.Fingerprint));
    }
}
