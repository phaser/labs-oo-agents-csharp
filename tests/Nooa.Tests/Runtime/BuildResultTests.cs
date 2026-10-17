// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.ContextBlocks;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class BuildResultTests
{
    [Fact]
    public void Constructor_KeepsBlocksAndCache()
    {
        var result = new BuildResult([new ResolvedBlock("a", "x")], new Dictionary<string, object?> { ["a"] = "x" });

        Assert.Single(result.Blocks);
        Assert.Equal("x", result.ResolvedCache["a"]);
    }
}
