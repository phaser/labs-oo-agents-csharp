// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Json;
using Nooa.Runtime;

namespace Nooa.Storage;

/// <summary>
/// <see cref="IEventBackend"/> backed by SQLite tables. Events are stored as JSON and restored through the
/// <see cref="EventBase"/> allowlist only. All methods take one lock, which the owning <see cref="SqliteStorageManager"/> shares.
/// </summary>
/// <remarks>
/// An unreadable or corrupt row is logged and skipped, as upstream does. A row with an <c>event_type</c> that is not on
/// the allowlist throws <see cref="DeserializationException"/> (upstream falls back to a generic Metadata event).
/// A row larger than the blob limit also throws.
/// </remarks>
public sealed class SqliteEventBackend : IEventBackend
{
    private readonly SqliteConnection _conn;
    private readonly Lock _lock;
    private readonly ILogger _logger;
    private readonly StorageLimits _limits;
    private long _insertionCounter;
    private long _nextTagNum;

    internal SqliteEventBackend(SqliteConnection conn, Lock lockObject, ILogger logger, StorageLimits limits)
    {
        _conn = conn;
        _lock = lockObject;
        _logger = logger;
        _limits = limits;
        _insertionCounter = MaxInsertionOrder() + 1;
        _nextTagNum = MaxTagNum() + 1;
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return (int)Scalar("SELECT COUNT(*) FROM events");
            }
        }
    }

    /// <inheritdoc />
    public void Store(string tag, EventBase eventToStore)
    {
        var data = Serialize(eventToStore);
        lock (_lock)
        {
            var order = _insertionCounter++;
            try
            {
                using var tx = _conn.BeginTransaction();
                Run(
                    "INSERT INTO events (tag, event_id, event_type, status, data, insertion_order) " +
                    "VALUES (@tag, @id, @type, @status, @data, @order)",
                    ("@tag", tag),
                    ("@id", eventToStore.Id),
                    ("@type", eventToStore.EventType),
                    ("@status", StatusText(eventToStore.Status)),
                    ("@data", data),
                    ("@order", order));
                Run("INSERT INTO active_tags (position, tag) VALUES (@pos, @tag)", ("@pos", MaxPosition() + 1), ("@tag", tag));
                tx.Commit();
            }
            catch (SqliteException e) when (SqliteSchema.IsCorruption(e))
            {
                _logger.LogError("Store({Tag}): database is corrupt: {Message}", tag, e.Message);
                throw new CorruptDatabaseException(e.Message, e);
            }

            _nextTagNum = Math.Max(_nextTagNum, EventTags.MaxNum(tag) + 1);
        }
    }

    /// <inheritdoc />
    public EventBase? Get(string tag) => ReadOne($"Get({tag})", "tag", tag);

    /// <inheritdoc />
    public EventBase? GetById(string eventId) => ReadOne($"GetById({eventId})", "event_id", eventId);

    /// <inheritdoc />
    public bool Update(string tag, Action<EventBase> mutate)
    {
        lock (_lock)
        {
            var found = ReadOne($"Update({tag})", "tag", tag);
            if (found is null)
            {
                return false;
            }

            mutate(found);
            SaveEvent(tag, found);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Remove(string tag)
    {
        lock (_lock)
        {
            if (!Guarded($"Remove({tag})", false, () => Query("SELECT tag FROM events WHERE tag = @tag", ("@tag", tag)).Count > 0))
            {
                return false;
            }

            using var tx = _conn.BeginTransaction();
            Run("DELETE FROM events WHERE tag = @tag", ("@tag", tag));
            Run("DELETE FROM active_tags WHERE tag = @tag", ("@tag", tag));
            tx.Commit();
            return true;
        }
    }

    /// <inheritdoc />
    public bool SetStatus(string tag, EventStatus status)
    {
        lock (_lock)
        {
            var found = ReadOne($"SetStatus({tag})", "tag", tag);
            if (found is null)
            {
                return false;
            }

            found.Status = status;
            SaveEvent(tag, found);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ActiveTags()
    {
        lock (_lock)
        {
            return Guarded<IReadOnlyList<string>>(
                "ActiveTags()",
                [],
                () => Query("SELECT tag FROM active_tags ORDER BY position").Select(r => (string)r[0]!).ToList());
        }
    }

    /// <inheritdoc />
    public void InsertActiveTag(string tag, int index)
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();
            Run("UPDATE active_tags SET position = position + 1 WHERE position >= @index", ("@index", index));
            Run("INSERT INTO active_tags (position, tag) VALUES (@index, @tag)", ("@index", index), ("@tag", tag));
            tx.Commit();
        }
    }

    /// <inheritdoc />
    public bool RemoveActiveTag(string tag)
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();
            var removed = Run("DELETE FROM active_tags WHERE tag = @tag", ("@tag", tag));
            tx.Commit();
            return removed > 0;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Rows are read in pages and deserialized one by one, outside the lock. The row count and the total size are checked
    /// while reading, so a large file cannot fill memory. A limit that is exceeded throws <see cref="DeserializationException"/>.
    /// </remarks>
    public IEnumerable<EventBase> AllEvents()
    {
        long lastOrder = long.MinValue;
        long lastRowId = long.MinValue;
        long count = 0;
        long bytes = 0;
        while (true)
        {
            List<object?[]> page;
            lock (_lock)
            {
                page = Guarded("AllEvents()", [], () => ReadPage(lastOrder, lastRowId));
            }

            if (page.Count == 0)
            {
                yield break;
            }

            foreach (var row in page)
            {
                count++;
                bytes += row[1] is long size ? size : 0;
                CheckTotals(count, bytes, "AllEvents()");
                lastOrder = (long)row[2]!;
                lastRowId = (long)row[3]!;
                if (DeserializeRow(row, "AllEvents()") is { } found)
                {
                    yield return found;
                }
            }
        }
    }

    /// <summary>Throws if the stored events are over the count or total size limit. It reads sizes only, not data.</summary>
    internal void EnforceLimits()
    {
        lock (_lock)
        {
            var row = Query("SELECT COUNT(*), COALESCE(SUM(length(CAST(data AS BLOB))), 0) FROM events")[0];
            CheckTotals((long)row[0]!, (long)row[1]!, "Open");
        }
    }

    private void CheckTotals(long count, long bytes, string context)
    {
        if (count > _limits.MaxEventCount)
        {
            throw new DeserializationException($"{context}: the database has more than {_limits.MaxEventCount} events.");
        }

        if (bytes > _limits.MaxTotalEventBytes)
        {
            throw new DeserializationException($"{context}: the event data is above the limit of {_limits.MaxTotalEventBytes} bytes.");
        }
    }

    // One page of rows after the given position. The page stops at the page byte budget, so memory stays bounded.
    private List<object?[]> ReadPage(long afterOrder, long afterRowId)
    {
        using var command = Command(
            BlobSelect($"ORDER BY insertion_order, rowid LIMIT {StorageLimits.PageRows}", "WHERE (insertion_order, rowid) > (@order, @rowid)"),
            [("@order", afterOrder), ("@rowid", afterRowId)]);
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        long pageBytes = 0;
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : ReadValue(reader, i);
            }

            rows.Add(row);
            pageBytes += row[1] is long size ? size : 0;
            if (pageBytes >= StorageLimits.PageBytes)
            {
                break;
            }
        }

        return rows;
    }

    /// <inheritdoc />
    public string? FindTag(EventBase eventToFind)
    {
        lock (_lock)
        {
            return Guarded<string?>(
                "FindTag()",
                null,
                () => Query("SELECT tag FROM events WHERE event_id = @id", ("@id", eventToFind.Id)).FirstOrDefault()?[0] as string);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();
            Run("DELETE FROM events");
            Run("DELETE FROM active_tags");
            tx.Commit();
            _insertionCounter = 0;
            _nextTagNum = 1;
        }
    }

    /// <inheritdoc />
    public string AllocateNextTag()
    {
        lock (_lock)
        {
            return (_nextTagNum++).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The highest tag number in storage, or 0 if there is none. A simple tag <c>5</c> gives 5 and a range tag <c>2..40</c> gives 40.
    /// </summary>
    public long MaxTagNum()
    {
        lock (_lock)
        {
            return Guarded(
                "MaxTagNum()",
                0L,
                () => Scalar(
                    "SELECT COALESCE(MAX(CAST(CASE WHEN instr(tag, '..') > 0 THEN substr(tag, instr(tag, '..') + 2) ELSE tag END AS INTEGER)), 0) FROM events"));
        }
    }

    private static string StatusText(EventStatus status) => status == EventStatus.Archived ? "archived" : "active";

    private static string Serialize(EventBase value) => JsonSerializer.Serialize(value, NooaJson.Options);

    // The row blob is read only when it is within the limit, so an oversized row is never loaded into memory.
    private string BlobSelect(string tail, string where = "") =>
        $"SELECT CASE WHEN length(CAST(data AS BLOB)) <= {_limits.MaxBlobBytes.ToString(CultureInfo.InvariantCulture)} THEN data END, " +
        $"length(CAST(data AS BLOB)), insertion_order, rowid FROM events {where} {tail}";

    private long MaxInsertionOrder() =>
        Guarded("MaxInsertionOrder()", -1L, () => Query("SELECT MAX(insertion_order) FROM events")[0][0] is long max ? max : -1L);

    private long MaxPosition() =>
        Guarded("MaxPosition()", -1L, () => Query("SELECT MAX(position) FROM active_tags")[0][0] is long max ? max : -1L);

    private EventBase? ReadOne(string context, string column, string value)
    {
        lock (_lock)
        {
            var row = Guarded(context, null, () => Query(BlobSelect("", $"WHERE {column} = @value"), ("@value", value)).FirstOrDefault());
            return row is null ? null : DeserializeRow(row, context);
        }
    }

    private IEnumerable<EventBase> DeserializeRows(List<object?[]> rows)
    {
        foreach (var row in rows)
        {
            if (DeserializeRow(row, "AllEvents()") is { } found)
            {
                yield return found;
            }
        }
    }

    private EventBase? DeserializeRow(object?[] row, string context)
    {
        if (row[0] is not string data)
        {
            var size = row[1] is long length ? length : -1;
            if (size > _limits.MaxBlobBytes)
            {
                throw new DeserializationException(
                    $"{context}: stored event is {size} bytes, above the limit of {_limits.MaxBlobBytes} bytes.");
            }

            _logger.LogWarning("{Context}: skipping corrupt event data (not text)", context);
            return null;
        }

        return TryDeserialize(data, context);
    }

    private EventBase? TryDeserialize(string data, string context)
    {
        string eventType;
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("event data JSON root must be an object");
            }

            if (!doc.RootElement.TryGetProperty("event_type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("event data has no event_type");
            }

            eventType = typeElement.GetString()!;
        }
        catch (JsonException e)
        {
            _logger.LogWarning("{Context}: skipping corrupt event data ({Reason})", context, e.Message);
            return null;
        }

        if (!EventTypeAllowlist.Contains(eventType))
        {
            throw new DeserializationException($"{context}: stored event has unknown event_type '{eventType}'. Only known event types can be restored.");
        }

        try
        {
            return JsonSerializer.Deserialize<EventBase>(data, NooaJson.Options);
        }
        catch (JsonException e)
        {
            _logger.LogWarning("{Context}: skipping corrupt event data ({Reason})", context, e.Message);
            return null;
        }
    }

    private void SaveEvent(string tag, EventBase changed)
    {
        using var tx = _conn.BeginTransaction();
        Run(
            "UPDATE events SET data = @data, status = @status WHERE tag = @tag",
            ("@data", Serialize(changed)),
            ("@status", StatusText(changed.Status)),
            ("@tag", tag));
        tx.Commit();
    }

    // Runs a read. A corruption error is logged and the fallback is returned. Any other error propagates.
    private T Guarded<T>(string context, T fallback, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (SqliteException e) when (SqliteSchema.IsCorruption(e))
        {
            _logger.LogWarning("{Context}: corrupt or unreadable database, using fallback ({Message})", context, e.Message);
            return fallback;
        }
    }

    private long Scalar(string sql)
    {
        using var command = _conn.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private int Run(string sql, params (string Name, object Value)[] args)
    {
        using var command = Command(sql, args);
        return command.ExecuteNonQuery();
    }

    private List<object?[]> Query(string sql, params (string Name, object Value)[] args)
    {
        using var command = Command(sql, args);
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : ReadValue(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static object ReadValue(SqliteDataReader reader, int i)
    {
        try
        {
            return reader.GetValue(i);
        }
        catch (InvalidCastException)
        {
            return reader.GetFieldValue<byte[]>(i);
        }
    }

    private SqliteCommand Command(string sql, (string Name, object Value)[] args)
    {
        var command = _conn.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }
}
