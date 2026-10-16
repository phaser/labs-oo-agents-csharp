// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_event_backend_roundtrip.py

using System.Reflection;
using System.Text.Json.Serialization;
using Nooa.ContextBlocks;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class EventTypeAllowlistTests
{
    [Fact]
    public void Contains_EveryDerivedTypeName_WithUniqueNames()
    {
        var names = typeof(EventBase).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => (string)a.TypeDiscriminator!).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.True(EventTypeAllowlist.Contains(name)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Metadata")]
    [InlineData("LLMOutput")]
    [InlineData("userevent")]
    [InlineData("System.Object")]
    public void Contains_UnknownName_IsFalse(string name) => Assert.False(EventTypeAllowlist.Contains(name));
}
