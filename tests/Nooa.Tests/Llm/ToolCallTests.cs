// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ToolCallTests
{
    [Fact]
    public void RoundTrip_InsideResponse_KeepsPart()
    {
        var part = new ToolCall { Id = "call_1", Name = "run", Arguments = "{}" };

        var back = EventJson.RoundTrip(new LlmResponse { Parts = [part] });

        Assert.Equal(part, Assert.Single(back.Parts));
    }
}
