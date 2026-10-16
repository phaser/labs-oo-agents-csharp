// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

using Microsoft.Data.Sqlite;

namespace Nooa.Storage;

internal static class SqliteSchema
{
    public const int Version = 1;

    // SQLITE_CORRUPT and SQLITE_NOTADB
    private const int _corruptCode = 11;
    private const int _notADatabaseCode = 26;

    // event_type and status are denormalized from the JSON data blob for indexed queries. All write paths must derive
    // these columns from the blob to keep them in sync. The JSON blob (data) is the source of truth.
    private const string _ddl = """
        CREATE TABLE IF NOT EXISTS schema_version (
            version INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS events (
            tag             TEXT PRIMARY KEY,
            event_id        TEXT NOT NULL,
            event_type      TEXT NOT NULL,
            status          TEXT NOT NULL DEFAULT 'active',
            data            TEXT NOT NULL,
            insertion_order INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_events_event_id ON events(event_id);
        CREATE INDEX IF NOT EXISTS idx_events_insertion_order ON events(insertion_order);
        CREATE INDEX IF NOT EXISTS idx_events_type_order ON events(event_type, insertion_order);

        CREATE TABLE IF NOT EXISTS active_tags (
            position INTEGER NOT NULL,
            tag      TEXT NOT NULL UNIQUE
        );

        CREATE TABLE IF NOT EXISTS snapshots (
            snapshot_id TEXT PRIMARY KEY,
            created_at  TEXT NOT NULL,
            data        TEXT NOT NULL
        );
        """;

    private static readonly string[] _corruptionMarkers =
    [
        "could not decode to utf-8",
        "database disk image is malformed",
        "file is not a database",
    ];

    /// <summary>Creates the tables if needed and checks the schema version. A newer or older file needs a migration.</summary>
    public static void Ensure(SqliteConnection conn)
    {
        using (var create = conn.CreateCommand())
        {
            create.CommandText = _ddl;
            create.ExecuteNonQuery();
        }

        using var read = conn.CreateCommand();
        read.CommandText = "SELECT version FROM schema_version";
        var found = read.ExecuteScalar();
        if (found is null)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO schema_version (version) VALUES (@version)";
            insert.Parameters.AddWithValue("@version", Version);
            insert.ExecuteNonQuery();
        }
        else if (Convert.ToInt64(found, System.Globalization.CultureInfo.InvariantCulture) != Version)
        {
            throw new InvalidDataException(
                $"SQLite schema version mismatch: DB has v{found}, code expects v{Version}. Migration required.");
        }
    }

    /// <summary>True for SQLite failures that corrupt or unreadable database content causes.</summary>
    public static bool IsCorruption(SqliteException exception) =>
        exception.SqliteErrorCode is _corruptCode or _notADatabaseCode
        || _corruptionMarkers.Any(m => exception.Message.Contains(m, StringComparison.OrdinalIgnoreCase));
}
