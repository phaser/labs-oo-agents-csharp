// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

using System.Text.Json;
using Nooa.ContextBlocks;
using Nooa.Json;

namespace Nooa.Tests.ContextBlocks;

public class ResultStatusTests
{
    [Theory]
    [InlineData(ResultStatus.Running, "running")]
    [InlineData(ResultStatus.Complete, "complete")]
    [InlineData(ResultStatus.Error, "error")]
    [InlineData(ResultStatus.Cancelled, "cancelled")]
    public void Serialize_UsesUpstreamValue(ResultStatus status, string expected)
    {
        Assert.Equal($"\"{expected}\"", JsonSerializer.Serialize(status, NooaJson.Options));
    }
}
