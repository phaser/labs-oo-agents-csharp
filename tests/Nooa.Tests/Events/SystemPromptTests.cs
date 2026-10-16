// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class SystemPromptTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new SystemPrompt { Content = "sys", GenerationId = "g" });

        Assert.Equal("SystemPrompt", back.EventType);
        Assert.Equal(Role.RuntimeEvent, back.EventRole);
        Assert.Equal(("sys", "g"), (back.Content, back.GenerationId));
    }
}
