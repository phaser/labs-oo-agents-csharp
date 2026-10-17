// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_models.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class BlockMetadataTests
{
    [Fact]
    public void Constructor_NoArguments_HasDefaultValues()
    {
        var metadata = new BlockMetadata();

        Assert.Null(metadata.Tag);
        Assert.False(metadata.Truncated);
        Assert.False(metadata.UserBlock);
        Assert.False(metadata.Static);
    }

    [Fact]
    public void With_ChangesOneField_KeepsTheOriginal()
    {
        var original = new BlockMetadata(Tag: "5", Static: true);

        var copy = original with { Truncated = true };

        Assert.False(original.Truncated);
        Assert.True(copy.Truncated);
        Assert.Equal("5", copy.Tag);
        Assert.True(copy.Static);
    }
}
