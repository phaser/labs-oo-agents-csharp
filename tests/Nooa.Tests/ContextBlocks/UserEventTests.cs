// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class UserEventTests
{
    [Fact]
    public void RoundTrip_KeepsContent()
    {
        var back = EventJson.RoundTrip(new UserEvent { Content = "What's the weather?" });

        Assert.Equal("What's the weather?", back.Content);
        Assert.Equal("UserEvent", back.EventType);
    }
}
