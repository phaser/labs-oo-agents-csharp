// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/storage/test_in_memory.py

using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Runtime;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class InMemoryStorageManagerTests
{
    [Fact]
    public void SaveSnapshotJson_Throws_StorageNotConfigured()
    {
        var e = Assert.Throws<StorageNotConfiguredException>(() => new InMemoryStorageManager().SaveSnapshotJson("{}"));

        Assert.Contains("does not support persistence", e.Message);
    }

    [Fact]
    public void LoadSnapshotJson_Throws_StorageNotConfigured() =>
        Assert.Throws<StorageNotConfiguredException>(() => new InMemoryStorageManager().LoadSnapshotJson("id"));

    [Fact]
    public void Snapshots_AreEmpty()
    {
        var storage = new InMemoryStorageManager();

        Assert.Null(storage.GetLatestSnapshotId());
        Assert.Empty(storage.ListSnapshots());
    }

    [Fact]
    public void EventBackend_IsInMemory_AndStable()
    {
        var storage = new InMemoryStorageManager();
        storage.EventBackend.Store("1", new UserEvent { Content = "a" });

        Assert.IsType<InMemoryEventBackend>(storage.EventBackend);
        Assert.Same(storage.EventBackend, storage.EventBackend);
        Assert.Equal(1, storage.EventBackend.Count);
    }
}
