// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class TaskEventTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new TaskEvent { Prompt = "Do something", Images = [new JsonObject { ["type"] = "image" }] });

        Assert.Equal("Task", back.EventType);
        Assert.Equal(Role.User, back.EventRole);
        Assert.Equal("Do something", back.Prompt); Assert.Equal("image", (string?)back.Images[0]["type"]);
    }
}
