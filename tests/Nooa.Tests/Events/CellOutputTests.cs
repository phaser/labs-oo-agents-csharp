// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.Events;

public class CellOutputTests
{
    [Fact]
    public void RoundTrip_KeepsFields_AndRole()
    {
        var back = EventJson.RoundTrip(new CellOutput { ToolCallId = "call_123", ExecutionStatus = ResultStatus.Error, ExecutionCount = 1, Stdout = "out", Stderr = "err", Error = "e", Value = 42, ExplicitReturn = true, Images = [new JsonObject { ["type"] = "image" }] });

        Assert.Equal("CellOutput", back.EventType);
        Assert.Equal(Role.User, back.EventRole);
        Assert.Equal(("call_123", ResultStatus.Error, 1, "out", "err", "e", true), (back.ToolCallId, back.ExecutionStatus, back.ExecutionCount, back.Stdout, back.Stderr, back.Error, back.ExplicitReturn)); Assert.Equal(42, (int?)back.Value); Assert.Single(back.Images);
    }

    [Fact]
    public void Defaults_OutputsEmpty_ValueNull()
    {
        var e = new CellOutput { ToolCallId = "c", ExecutionStatus = ResultStatus.Complete, ExecutionCount = 1 };

        Assert.Equal(("", "", ""), (e.Stdout, e.Stderr, e.Error));
        Assert.Null(e.Value);
        Assert.False(e.ExplicitReturn);
        Assert.Empty(e.Images);
    }
}
