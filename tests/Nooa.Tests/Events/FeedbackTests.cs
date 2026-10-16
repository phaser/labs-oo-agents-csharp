// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class FeedbackTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new Feedback { Content = "Code executed. Output: 42" });

        Assert.Equal("Feedback", back.EventType);
        Assert.Equal(Role.User, back.EventRole);
        Assert.Equal("Code executed. Output: 42", back.Content);
    }
}
