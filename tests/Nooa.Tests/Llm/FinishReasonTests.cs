// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class FinishReasonTests
{
    [Theory]
    [InlineData(FinishReason.Stop, "stop")]
    [InlineData(FinishReason.ToolCalls, "tool_calls")]
    [InlineData(FinishReason.Length, "length")]
    [InlineData(FinishReason.Error, "error")]
    public void Serialize_UsesPortableValue(FinishReason reason, string expected)
    {
        Assert.Contains($"\"finish_reason\":\"{expected}\"", EventJson.Write(new LlmResponse { FinishReason = reason }));
    }
}
