// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class TextOnlyReplyTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new TextOnlyReply { Content = "hi", FinishReason = "stop", Handler = "h", Action = "retry", ConsecutiveTextOnly = 2 });

        Assert.Equal("TextOnlyReply", back.EventType);
        Assert.Equal(Role.Metadata, back.EventRole);
        Assert.Equal(("hi", "stop", "h", "retry", 2), (back.Content, back.FinishReason, back.Handler, back.Action, back.ConsecutiveTextOnly));
    }
}
