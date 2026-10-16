// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_event_backend_protocol.py

using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class EventTagsTests
{
    [Theory]
    [InlineData("5", 5)]
    [InlineData("2..40", 40)]
    [InlineData("233..293432", 293432)]
    [InlineData("abc", 0)]
    [InlineData("1..x", 0)]
    [InlineData("", 0)]
    public void MaxNum_ParsesTheHighestNumber(string tag, long expected) => Assert.Equal(expected, EventTags.MaxNum(tag));
}
