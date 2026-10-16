// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/manager.py

using Nooa.Runtime;

namespace Nooa.Storage;

/// <summary>
/// The one object that gives an agent persistence: it owns the event backend and stores snapshot blobs.
/// </summary>
/// <remarks>
/// The agent owns its event manager. Storage owns only the event backend under it, so swapping storage keeps the
/// subscribers and middleware of the agent. A snapshot is a JSON text; the id format is up to the implementation.
/// Upstream <c>save_snapshot(agent)</c> and <c>restore_snapshot(id, agent)</c> read and write the agent fields, so they
/// are added with the agent in T12. They call <see cref="SaveSnapshotJson"/> and <see cref="LoadSnapshotJson"/>.
/// </remarks>
public interface IStorageManager
{
    /// <summary>The backend that the event manager writes through.</summary>
    IEventBackend EventBackend { get; }

    /// <summary>
    /// Stores a snapshot JSON text and returns its id. The id defaults to a new GUID.
    /// </summary>
    /// <exception cref="Nooa.Errors.StorageNotConfiguredException">The storage cannot persist.</exception>
    string SaveSnapshotJson(string data, string? snapshotId = null, string? createdAt = null);

    /// <summary>
    /// Returns the JSON text of a snapshot. The text is checked for size and JSON syntax. The caller must still check the
    /// snapshot <c>version</c> and the types before it restores values.
    /// </summary>
    /// <exception cref="Nooa.Errors.SnapshotNotFoundException">The id is not in storage.</exception>
    /// <exception cref="Nooa.Errors.StorageNotConfiguredException">The storage cannot persist.</exception>
    /// <exception cref="Nooa.Errors.DeserializationException">The stored text is too large or is not a JSON object.</exception>
    string LoadSnapshotJson(string snapshotId);

    /// <summary>Returns the id of the most recently saved snapshot, or null if there is none.</summary>
    string? GetLatestSnapshotId();

    /// <summary>Lists all stored snapshots, oldest first.</summary>
    IReadOnlyList<SnapshotInfo> ListSnapshots();
}
