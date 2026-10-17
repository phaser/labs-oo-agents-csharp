// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_out_accessor.py

using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class OutAccessorTests
{
    private static OutAccessor Filled()
    {
        var accessor = new OutAccessor();
        accessor.Record(1, "a");
        accessor.Record(2, null);
        accessor.Record(3, 30);
        accessor.Record(5, 50);
        return accessor;
    }

    [Fact]
    public void Indexer_Empty_ThrowsIndexOutOfRange()
    {
        var accessor = new OutAccessor();

        Assert.Throws<IndexOutOfRangeException>(() => accessor[1]);
        Assert.Throws<IndexOutOfRangeException>(() => accessor[-1]);
        Assert.Throws<InvalidOperationException>(() => accessor.Last);
        Assert.Equal("Out (no outputs yet)", accessor.ToString());
    }

    [Fact]
    public void Record_NullValue_IsNotStored()
    {
        Assert.Equal(3, Filled().Count);
    }

    [Fact]
    public void Indexer_PositiveIndex_FindsTheCellNumberEvenWhenSparse()
    {
        var accessor = Filled();

        Assert.Equal(30, accessor[3]);
        Assert.Equal(50, accessor[5]);
        Assert.Throws<KeyNotFoundException>(() => accessor[2]);
        Assert.Throws<KeyNotFoundException>(() => accessor[4]);
    }

    [Fact]
    public void Indexer_NegativeIndex_CountsFromTheEnd()
    {
        var accessor = Filled();

        Assert.Equal(50, accessor[-1]);
        Assert.Equal(30, accessor[-2]);
        Assert.Equal("a", accessor[-3]);
        var error = Assert.Throws<IndexOutOfRangeException>(() => accessor[-4]);
        Assert.Contains("have 3 outputs", error.Message);
    }

    [Fact]
    public void Contains_ChecksTheCellNumber()
    {
        var accessor = Filled();

        Assert.True(accessor.Contains(3));
        Assert.False(accessor.Contains(2));
        Assert.Equal(50, accessor.Last);
    }

    [Fact]
    public void ToString_ListsEveryValue()
    {
        var text = Filled().ToString();

        Assert.Equal("Out[1]: a\nOut[3]: 30\nOut[5]: 50", text);
    }

    [Fact]
    public void ToString_LongValue_IsCut()
    {
        var accessor = new OutAccessor();
        accessor.Record(1, Enumerable.Range(0, 5_000).ToList());

        Assert.True(accessor.ToString().Length < 1_000);
    }
}
