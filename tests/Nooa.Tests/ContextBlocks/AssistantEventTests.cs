// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class AssistantEventTests
{
    [Fact]
    public void RoundTrip_KeepsContent()
    {
        var back = EventJson.RoundTrip(new AssistantEvent { Content = "The weather is sunny." });

        Assert.Equal("The weather is sunny.", back.Content);
        Assert.Equal("AssistantEvent", back.EventType);
    }
}
