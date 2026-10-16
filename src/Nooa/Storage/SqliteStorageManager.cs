// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Errors;
using Nooa.Runtime;

namespace Nooa.Storage;

/// <summary>
/// <see cref="IStorageManager"/> backed by a SQLite database. It gives persistent event storage and snapshot blobs.
/// Dispose it to release the connection and the session lock.
/// </summary>
/// <remarks>
/// <para>
/// A file database takes an exclusive lock (<c>&lt;file&gt;.lock.guard</c>, with the owner record in <c>&lt;file&gt;.lock</c>), so
/// that two processes do not write one session. Use <c>:memory:</c> (the default) for tests.
/// </para>
/// <para>
/// The file can come from an untrusted source. The manager checks the schema version when it opens the file, never
/// restores an event type outside the <see cref="Nooa.ContextBlocks.EventBase"/> allowlist, and refuses a stored blob
/// larger than the blob limit. All SQL is parameterized.
/// </para>
/// <para>
/// Not ported: virtiofs detection (so the default journal mode is always WAL), and the reconnect on a disk I/O error.
/// </para>
/// </remarks>
public sealed class SqliteStorageManager : IStorageManager, IDisposable
{
    /// <summary>The default limit for one stored event or snapshot: 64 MiB.</summary>
    public const long DefaultMaxBlobBytes = 64L * 1024 * 1024;

    private const string _memoryPath = ":memory:";

    private readonly string _dbPath;
    private readonly Lock _dbLock = new();
    private readonly SqliteConnection _conn;
    private readonly SessionLock? _sessionLock;
    private readonly long _maxBlobBytes;
    private bool _closed;

    /// <summary>Opens or creates a session database.</summary>
    /// <param name="dbPath">Path of the database file, or <c>:memory:</c>.</param>
    /// <param name="mustExist">Refuse a missing file instead of creating it, and create nothing (no lock file either). Not valid with <c>:memory:</c>.</param>
    /// <param name="journalMode">Journal mode. Null uses WAL.</param>
    /// <param name="logger">Receives warnings about skipped rows.</param>
    /// <param name="maxBlobBytes">The largest stored event or snapshot that the manager reads or writes.</param>
    /// <exception cref="SessionAlreadyActiveException">The file is open in another process or manager.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="mustExist"/> is set and the file is missing.</exception>
    /// <exception cref="ArgumentException">A bad option value.</exception>
    public SqliteStorageManager(
        string dbPath = _memoryPath,
        bool mustExist = false,
        SqliteJournalMode? journalMode = null,
        ILogger? logger = null,
        long maxBlobBytes = DefaultMaxBlobBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(dbPath);
        if (journalMode is { } mode && !Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(journalMode), journalMode, "journalMode must be Wal, Delete or null.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBlobBytes);
        var isMemory = dbPath == _memoryPath;
        if (mustExist && isMemory)
        {
            throw new ArgumentException("mustExist cannot be used with an in-memory database.", nameof(mustExist));
        }

        // A full path is never read as a SQLite URI.
        _dbPath = isMemory ? _memoryPath : Path.GetFullPath(dbPath);
        _maxBlobBytes = maxBlobBytes;

        if (!isMemory)
        {
            // Check before locking so a missing file (or directory) creates no stray lock file.
            if (mustExist && !File.Exists(_dbPath))
            {
                throw new FileNotFoundException($"unable to open database file: {_dbPath}", _dbPath);
            }

            _sessionLock = SessionLock.Acquire(LockPath(_dbPath));
        }

        _conn = OpenConnection(mustExist, journalMode);
        try
        {
            SqliteSchema.Ensure(_conn);
            EventBackend = new SqliteEventBackend(_conn, _dbLock, logger ?? NullLogger.Instance, maxBlobBytes);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public IEventBackend EventBackend { get; }

    internal SqliteConnection Connection => _conn;

    /// <summary>The path of the database file, or null for in-memory storage.</summary>
    public string? DatabasePath => _dbPath == _memoryPath ? null : _dbPath;

    /// <summary>
    /// Returns the process id and host that the owner of a lock file wrote, or <c>(null, null)</c> if the file is missing,
    /// blank or not parseable. A blank file means the owner closed cleanly.
    /// </summary>
    public static (int? Pid, string? Host) ReadLockOwner(string lockPath) => SessionLock.ReadOwner(lockPath);

    /// <summary>
    /// Deletes an inactive database and its WAL and SHM files. The session lock is held for the whole deletion, so a live
    /// session throws <see cref="SessionAlreadyActiveException"/>. The lock file stays on disk with a blank record.
    /// Returns true if the database file existed.
    /// </summary>
    public static bool DeleteDatabase(string dbPath)
    {
        if (dbPath == _memoryPath)
        {
            throw new ArgumentException("An in-memory SQLite database cannot be deleted.", nameof(dbPath));
        }

        var fullPath = Path.GetFullPath(dbPath);
        if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
        {
            return false;
        }

        using var held = SessionLock.Acquire(LockPath(fullPath));
        var existed = File.Exists(fullPath);
        foreach (var candidate in new[] { fullPath, fullPath + "-wal", fullPath + "-shm" })
        {
            File.Delete(candidate);
        }

        return existed;
    }

    /// <inheritdoc />
    public string SaveSnapshotJson(string data, string? snapshotId = null, string? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var size = System.Text.Encoding.UTF8.GetByteCount(data);
        if (size > _maxBlobBytes)
        {
            throw new ArgumentException($"Snapshot is {size} bytes, above the limit of {_maxBlobBytes} bytes.", nameof(data));
        }

        RequireJsonObject(data, msg => new ArgumentException(msg, nameof(data)));
        snapshotId ??= Guid.NewGuid().ToString();
        createdAt ??= DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        lock (_dbLock)
        {
            using var command = _conn.CreateCommand();
            command.CommandText = "INSERT INTO snapshots (snapshot_id, created_at, data) VALUES (@id, @created, @data)";
            command.Parameters.AddWithValue("@id", snapshotId);
            command.Parameters.AddWithValue("@created", createdAt);
            command.Parameters.AddWithValue("@data", data);
            command.ExecuteNonQuery();
        }

        return snapshotId;
    }

    /// <inheritdoc />
    public string LoadSnapshotJson(string snapshotId)
    {
        string? data;
        long size;
        lock (_dbLock)
        {
            using var command = _conn.CreateCommand();

            // The blob is read only when it is within the limit.
            command.CommandText =
                $"SELECT CASE WHEN length(CAST(data AS BLOB)) <= {_maxBlobBytes.ToString(CultureInfo.InvariantCulture)} THEN data END, " +
                "length(CAST(data AS BLOB)) FROM snapshots WHERE snapshot_id = @id";
            command.Parameters.AddWithValue("@id", snapshotId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new SnapshotNotFoundException($"Snapshot '{snapshotId}' not found");
            }

            data = reader.IsDBNull(0) ? null : reader.GetString(0);
            size = reader.GetInt64(1);
        }

        if (data is null)
        {
            throw new DeserializationException($"Snapshot '{snapshotId}' is {size} bytes, above the limit of {_maxBlobBytes} bytes.");
        }

        RequireJsonObject(data, msg => new DeserializationException($"Snapshot '{snapshotId}': {msg}"));
        return data;
    }

    /// <inheritdoc />
    public string? GetLatestSnapshotId()
    {
        lock (_dbLock)
        {
            using var command = _conn.CreateCommand();
            command.CommandText = "SELECT snapshot_id FROM snapshots ORDER BY created_at DESC, rowid DESC LIMIT 1";
            return command.ExecuteScalar() as string;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SnapshotInfo> ListSnapshots()
    {
        lock (_dbLock)
        {
            using var command = _conn.CreateCommand();
            command.CommandText = "SELECT snapshot_id, created_at FROM snapshots ORDER BY created_at, rowid";
            using var reader = command.ExecuteReader();
            var list = new List<SnapshotInfo>();
            while (reader.Read())
            {
                list.Add(new SnapshotInfo(reader.GetString(0), reader.GetString(1)));
            }

            return list;
        }
    }

    /// <summary>Closes the connection and releases the session lock. It is safe to call more than once.</summary>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        try
        {
            lock (_dbLock)
            {
                _conn?.Dispose();
            }
        }
        finally
        {
            _sessionLock?.Dispose();
        }
    }

    private static string LockPath(string dbPath) => Path.ChangeExtension(dbPath, ".lock");

    private static void RequireJsonObject<TException>(string data, Func<string, TException> fail)
        where TException : Exception
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw fail("the snapshot JSON root must be an object");
            }
        }
        catch (JsonException e)
        {
            throw fail($"the snapshot is not valid JSON ({e.Message})");
        }
    }

    private SqliteConnection OpenConnection(bool mustExist, SqliteJournalMode? journalMode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = mustExist ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate,

            // A pooled connection would keep the file open after Dispose.
            Pooling = false,
        };
        var conn = new SqliteConnection(builder.ToString());
        try
        {
            conn.Open();

            // Retry up to 5 s on SQLITE_BUSY before an error.
            Pragma(conn, "busy_timeout=5000");
            Pragma(conn, journalMode == SqliteJournalMode.Delete ? "journal_mode=DELETE" : "journal_mode=WAL");

            // FULL in every mode: with NORMAL, a disk-full during a WAL commit can persist partial pages.
            Pragma(conn, "synchronous=FULL");
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    private static void Pragma(SqliteConnection conn, string setting)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "PRAGMA " + setting;
        command.ExecuteNonQuery();
    }
}
