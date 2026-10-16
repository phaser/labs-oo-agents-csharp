// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/in_memory.py

using Nooa.Errors;
using Nooa.Runtime;

namespace Nooa.Storage;

/// <summary>
/// Storage manager that keeps everything in memory. It is the default when no storage is configured.
/// Events live in an <see cref="InMemoryEventBackend"/>. Snapshot save and load throw, because there is nowhere to persist to.
/// </summary>
public sealed class InMemoryStorageManager : IStorageManager
{
    private const string _notConfigured =
        "InMemoryStorageManager does not support persistence. Pass a persistent StorageManager to enable save/restore.";

    /// <inheritdoc />
    public IEventBackend EventBackend { get; } = new InMemoryEventBackend();

    /// <inheritdoc />
    public string SaveSnapshotJson(string data, string? snapshotId = null, string? createdAt = null) =>
        throw new StorageNotConfiguredException(_notConfigured);

    /// <inheritdoc />
    public string LoadSnapshotJson(string snapshotId) => throw new StorageNotConfiguredException(_notConfigured);

    /// <inheritdoc />
    public string? GetLatestSnapshotId() => null;

    /// <inheritdoc />
    public IReadOnlyList<SnapshotInfo> ListSnapshots() => [];
}
