// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class NotificationTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new Notification { Source = "queue:user_messages", Description = "d", Value = new JsonObject { ["k"] = 1 } });

        Assert.Equal("Notification", back.EventType);
        Assert.Equal(Role.User, back.EventRole);
        Assert.Equal(("queue:user_messages", "d"), (back.Source, back.Description)); Assert.Equal(1, (int?)back.Value!["k"]);
    }
}
