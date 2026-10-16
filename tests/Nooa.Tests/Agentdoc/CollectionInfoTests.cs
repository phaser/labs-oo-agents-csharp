// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class CollectionInfoTests
{
    [Fact]
    public void Count_Dictionary_CountsEntries()
    {
        var info = new CollectionInfo(CollectionKind.Dictionary, "d", [], [new("a", 1), new("b", 2)]);
        Assert.Equal(2, info.Count);
    }

    [Fact]
    public void Count_List_CountsItems()
    {
        var info = new CollectionInfo(CollectionKind.List, "l", [1, 2, 3], []);
        Assert.Equal(3, info.Count);
    }
}
