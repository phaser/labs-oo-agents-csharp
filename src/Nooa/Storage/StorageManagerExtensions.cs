// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/manager.py

namespace Nooa.Storage;

/// <summary>
/// Agent-level snapshot operations on top of the blob API of <see cref="IStorageManager"/>
/// (upstream <c>save_snapshot</c>, <c>restore_snapshot</c> and <c>restore_latest_snapshot</c>).
/// </summary>
public static class StorageManagerExtensions
{
    /// <summary>Saves the state of an agent and returns the snapshot id.</summary>
    /// <exception cref="Nooa.Errors.StorageNotConfiguredException">The storage cannot persist.</exception>
    /// <exception cref="Nooa.Errors.SerializationException">A context block value is not JSON-like.</exception>
    public static string SaveSnapshot(this IStorageManager storage, Agent agent)
    {
        ArgumentNullException.ThrowIfNull(storage);
        return storage.SaveSnapshotJson(SnapshotJson.ToJson(agent));
    }

    /// <summary>Restores a saved snapshot into a freshly built agent.</summary>
    /// <exception cref="Nooa.Errors.SnapshotNotFoundException">The id is not in storage.</exception>
    /// <exception cref="Nooa.Errors.SerializationException">The snapshot version is not supported.</exception>
    /// <exception cref="Nooa.Errors.DeserializationException">The snapshot is refused.</exception>
    public static void RestoreSnapshot(this IStorageManager storage, string snapshotId, Agent agent)
    {
        ArgumentNullException.ThrowIfNull(storage);
        SnapshotJson.FromJson(storage.LoadSnapshotJson(snapshotId), agent);
    }

    /// <summary>Restores the most recent snapshot. Returns false when the storage has no snapshot.</summary>
    public static bool RestoreLatestSnapshot(this IStorageManager storage, Agent agent)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (storage.GetLatestSnapshotId() is not { } id)
        {
            return false;
        }

        storage.RestoreSnapshot(id, agent);
        return true;
    }
}
