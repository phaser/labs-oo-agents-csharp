// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.ContextBlocks;

public class EventStatusTests
{
    [Fact]
    public void Status_DefaultsToActive_AndPersistsAsText()
    {
        var e = new Message { Content = "x" };
        Assert.Equal(EventStatus.Active, e.Status);
        Assert.Contains("\"status\":\"active\"", EventJson.Write(e));

        e.Status = EventStatus.Archived;
        Assert.Equal(EventStatus.Archived, EventJson.RoundTrip(e).Status);
    }
}
