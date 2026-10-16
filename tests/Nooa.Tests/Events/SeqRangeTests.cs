// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_events.py

using System.Text.Json;
using Nooa.Events;
using Nooa.Json;

namespace Nooa.Tests.Events;

public class SeqRangeTests
{
    [Theory]
    [InlineData("[1]")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,\"x\"]")]
    public void Deserialize_Malformed_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SeqRange>(json, NooaJson.Options));
    }

    [Fact]
    public void Deserialize_Pair_ReadsBothValues()
    {
        Assert.Equal(new SeqRange(5, 10), JsonSerializer.Deserialize<SeqRange>("[5,10]", NooaJson.Options));
    }
}
