// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_events.py

using System.Text.Json;
using Nooa.ContextBlocks;
using Nooa.Json;

namespace Nooa.Tests.ContextBlocks;

public class ToolResultTests
{
    [Fact]
    public void Constructor_StatusDefaultsToComplete()
    {
        var result = new ToolResult { ToolCallId = "call_123", Content = "Sunny, 72°F" };

        Assert.Equal("call_123", result.ToolCallId);
        Assert.Equal("Sunny, 72°F", result.Content);
        Assert.Equal(ResultStatus.Complete, result.ResultStatus);
    }

    [Fact]
    public void Serialize_ErrorStatus_RoundTrips()
    {
        var result = new ToolResult { ToolCallId = "c", Content = "Error occurred", ResultStatus = ResultStatus.Error };

        var json = JsonSerializer.Serialize(result, NooaJson.Options);
        var back = JsonSerializer.Deserialize<ToolResult>(json, NooaJson.Options)!;

        Assert.Contains("\"result_status\":\"error\"", json);
        Assert.Equal(result, back);
    }
}
