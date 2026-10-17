// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_models.py

using Nooa.ContextBlocks;
using Nooa.Events;

namespace Nooa.Tests.ContextBlocks;

public class ResolvedBlockTests
{
    [Fact]
    public void Constructor_Minimal_HasSystemRoleAndDefaultMetadata()
    {
        var block = new ResolvedBlock("notes", "hello");

        Assert.Equal(Role.System, block.Role);
        Assert.Equal(new BlockMetadata(), block.Metadata);
        Assert.Null(block.Event);
    }

    [Fact]
    public void Constructor_WithEvent_CarriesTheEvent()
    {
        var task = new TaskEvent { Prompt = "p" };

        var block = new ResolvedBlock("event_1", "", Role.User, new BlockMetadata(Tag: "1"), task);

        Assert.Same(task, block.Event);
        Assert.Equal("1", block.Metadata.Tag);
    }

    [Fact]
    public void With_ReplacesContent_KeepsTheOriginal()
    {
        var original = new ResolvedBlock("k", "old", Metadata: new BlockMetadata(UserBlock: true));

        var copy = original with { Content = "new" };

        Assert.Equal("old", original.Content);
        Assert.Equal("new", copy.Content);
        Assert.True(copy.Metadata.UserBlock);
    }
}
