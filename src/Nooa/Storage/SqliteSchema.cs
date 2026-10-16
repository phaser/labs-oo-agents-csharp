// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

using Microsoft.Data.Sqlite;
using Nooa.Errors;

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

    /// <summary>
    /// Refuses a database that holds any object other than the expected tables and indexes. An untrusted file can carry
    /// triggers, views or changed table definitions that run when this program queries or writes. Every object that exists
    /// must match the object that the DDL creates in an empty database. A missing object is allowed: <see cref="Ensure"/> creates it.
    /// </summary>
    public static void Verify(SqliteConnection conn)
    {
        using var reference = new SqliteConnection("Data Source=:memory:");
        reference.Open();
        using (var create = reference.CreateCommand())
        {
            create.CommandText = _ddl;
            create.ExecuteNonQuery();
        }

        var allowed = ReadObjects(reference);
        foreach (var found in ReadObjects(conn))
        {
            if (found.Type is not ("table" or "index"))
            {
                throw new StorageSchemaException(
                    $"The database holds an unexpected {found.Type} '{found.Name}'. Triggers and views are not allowed in a Nooa session database.");
            }

            if (!allowed.Contains(found))
            {
                throw new StorageSchemaException(
                    $"The database holds an unexpected {found.Type} '{found.Name}'. The file is not a Nooa session database or was changed.");
            }
        }
    }

    // Keyed by every column, not by name: SQLite keeps trigger names apart from table names, so a trigger can share a
    // name with a table. A set of full rows cannot hide one object behind another.
    private static HashSet<(string Type, string Name, string Table, string? Sql)> ReadObjects(SqliteConnection conn)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "SELECT type, name, tbl_name, sql FROM sqlite_master";
        using var reader = command.ExecuteReader();
        var objects = new HashSet<(string, string, string, string?)>();
        while (reader.Read())
        {
            objects.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return objects;
    }

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
