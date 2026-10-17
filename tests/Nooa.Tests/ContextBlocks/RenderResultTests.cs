// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class RenderResultTests
{
    [Fact]
    public void Constructor_KeepsMessagesAndStats()
    {
        var stats = new ContextWindowStats { ContextBlocksCount = 1, EventsCount = 2 };

        var result = new RenderResult([], stats);

        Assert.Empty(result.Messages);
        Assert.Same(stats, result.Stats);
    }
}
