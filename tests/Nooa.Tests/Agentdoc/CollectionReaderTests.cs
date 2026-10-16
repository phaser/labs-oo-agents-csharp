// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_pformat_head_tail.py

using System.Collections.Immutable;
using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class CollectionReaderTests
{
    [Fact]
    public void TryRead_List_ReturnsItemsInOrder()
    {
        var info = CollectionReader.TryRead(new List<int> { 3, 1, 2 });
        Assert.NotNull(info);
        Assert.Equal(CollectionKind.List, info.Kind);
        Assert.Equal("List<int>", info.TypeName);
        Assert.Equal([3, 1, 2], info.Items);
    }

    [Fact]
    public void TryRead_ArrayAndImmutableArray_AreLists()
    {
        Assert.Equal(CollectionKind.List, CollectionReader.TryRead(new[] { 1 })!.Kind);
        Assert.Equal(CollectionKind.List, CollectionReader.TryRead(ImmutableArray.Create(1))!.Kind);
        Assert.Equal(CollectionKind.List, CollectionReader.TryRead(new Queue<int>([1]))!.Kind);
    }

    [Fact]
    public void TryRead_Tuple_ReturnsElements()
    {
        var info = CollectionReader.TryRead((1, "a"));
        Assert.NotNull(info);
        Assert.Equal(CollectionKind.Tuple, info.Kind);
        Assert.Equal([1, "a"], info.Items);
    }

    [Fact]
    public void TryRead_Sets_AreSets()
    {
        Assert.Equal(CollectionKind.Set, CollectionReader.TryRead(new HashSet<int> { 1 })!.Kind);
        Assert.Equal(CollectionKind.Set, CollectionReader.TryRead(ImmutableHashSet.Create(1))!.Kind);
    }

    [Fact]
    public void TryRead_Dictionary_ReturnsEntries()
    {
        var info = CollectionReader.TryRead(new Dictionary<string, int> { ["a"] = 1 });
        Assert.NotNull(info);
        Assert.Equal(CollectionKind.Dictionary, info.Kind);
        Assert.Equal(1, info.Count);
        Assert.Equal("a", info.Entries[0].Key);
        Assert.Equal(1, info.Entries[0].Value);
    }

    [Fact]
    public void TryRead_ImmutableDictionary_IsDictionary()
    {
        Assert.Equal(CollectionKind.Dictionary, CollectionReader.TryRead(ImmutableDictionary<string, int>.Empty.Add("a", 1))!.Kind);
    }

    [Fact]
    public void TryRead_LazySequenceStringAndObject_ReturnNull()
    {
        Assert.Null(CollectionReader.TryRead(Enumerable.Range(0, 3).Select(i => i)));
        Assert.Null(CollectionReader.TryRead("abc"));
        Assert.Null(CollectionReader.TryRead(new object()));
    }
}
