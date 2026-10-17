// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_scoped.py

using Nooa.ContextBlocks;
using Nooa.Runtime;

namespace Nooa.Tests.ContextBlocks;

public class ScopedContextTests
{
    private static Dictionary<string, object?> Blocks(params (string Key, object? Value)[] items) =>
        items.ToDictionary(i => i.Key, i => i.Value);

    [Fact]
    public void Enter_SetsTheBlocks_AndDisposeCleansUp()
    {
        Assert.Null(ScopedContext.CurrentBlocks);

        using (new ScopedContext(Blocks(("focus", "security"))).Enter())
        {
            Assert.Equal("security", ScopedContext.CurrentBlocks!["focus"]);
        }

        Assert.Null(ScopedContext.CurrentBlocks);
    }

    [Fact]
    public void Enter_Nested_InheritsTheParent()
    {
        using (new ScopedContext(Blocks(("a", "1"))).Enter())
        using (new ScopedContext(Blocks(("b", "2"))).Enter())
        {
            Assert.Equal(["a", "b"], ScopedContext.CurrentBlocks!.Keys.Order());
        }
    }

    [Fact]
    public void Enter_Nested_OverridesTheParent_AndRestoresItOnExit()
    {
        using (new ScopedContext(Blocks(("a", "outer"))).Enter())
        {
            using (new ScopedContext(Blocks(("a", "inner"))).Enter())
            {
                Assert.Equal("inner", ScopedContext.CurrentBlocks!["a"]);
            }

            Assert.Equal("outer", ScopedContext.CurrentBlocks!["a"]);
        }
    }

    [Fact]
    public void Enter_NullValue_RemovesTheBlockInTheScope()
    {
        using (new ScopedContext(Blocks(("a", "1"))).Enter())
        using (new ScopedContext(Blocks(("a", null))).Enter())
        {
            Assert.True(ScopedContext.CurrentBlocks!.ContainsKey("a"));
            Assert.Null(ScopedContext.CurrentBlocks["a"]);
        }
    }

    [Fact]
    public void Enter_DynamicValue_IsKept()
    {
        var dynamic = new DynamicContext(() => "x");

        using (new ScopedContext(Blocks(("a", dynamic))).Enter())
        {
            Assert.Same(dynamic, ScopedContext.CurrentBlocks!["a"]);
        }
    }

    [Fact]
    public void Enter_EmptyScope_SetsAnEmptyMap()
    {
        using (new ScopedContext().Enter())
        {
            Assert.Empty(ScopedContext.CurrentBlocks!);
        }
    }

    [Fact]
    public void Enter_Events_ChildReplacesParent_AndNullKeepsTheParent()
    {
        var outer = EventQuery.LastN(10);
        var inner = EventQuery.CurrentCall();

        using (new ScopedContext(events: outer).Enter())
        {
            using (new ScopedContext().Enter())
            {
                Assert.Same(outer, ScopedContext.CurrentEvents);
            }

            using (new ScopedContext(events: inner).Enter())
            {
                Assert.Same(inner, ScopedContext.CurrentEvents);
            }

            Assert.Same(outer, ScopedContext.CurrentEvents);
        }

        Assert.Null(ScopedContext.CurrentEvents);
    }

    [Fact]
    public async Task Enter_ParallelTasks_InheritTheScopeAndDoNotLeakBetweenSiblings()
    {
        using (new ScopedContext(Blocks(("a", "1"))).Enter())
        {
            var inherited = await Task.Run(() => ScopedContext.CurrentBlocks!["a"]);
            var sibling = await Task.Run(() =>
            {
                using (new ScopedContext(Blocks(("b", "2"))).Enter())
                {
                    return ScopedContext.CurrentBlocks!.Count;
                }
            });

            Assert.Equal("1", inherited);
            Assert.Equal(2, sibling);
            Assert.Single(ScopedContext.CurrentBlocks!);
        }
    }

    [Fact]
    public void Dispose_Twice_IsSafe()
    {
        var scope = new ScopedContext(Blocks(("a", "1"))).Enter();

        scope.Dispose();
        scope.Dispose();

        Assert.Null(ScopedContext.CurrentBlocks);
    }
}
