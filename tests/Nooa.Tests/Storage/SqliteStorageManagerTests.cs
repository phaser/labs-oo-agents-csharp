// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/storage/test_sqlite_public_options.py, tests/test_sqlite_storage_latest.py, tests/test_sqlite_close_commit.py, tests/test_sqlite_corruption_resilience.py, tests/test_sqlite_reconnect.py

using Microsoft.Data.Sqlite;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class SqliteStorageManagerTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private static UserEvent User(string content) => new() { Content = content };

    private SqliteConnection Raw(string name)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dir.File(name), Pooling = false }.ToString());
        conn.Open();
        return conn;
    }

    private string Scalar(string name, string sql)
    {
        using var conn = Raw(name);
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    // -- snapshots --

    [Fact]
    public void SaveSnapshotJson_WritesTheGivenBlob()
    {
        string first;
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            first = storage.SaveSnapshotJson("{\"a\":1}");
            Assert.Equal(first, storage.GetLatestSnapshotId());
            var pinned = storage.SaveSnapshotJson("{\"a\":1}", "fixed", "2999-01-01T00:00:00+00:00");
            Assert.Equal("fixed", pinned);
            Assert.Equal("fixed", storage.GetLatestSnapshotId());
        }

        Assert.Equal("{\"a\":1}", Scalar("s.db", $"SELECT data FROM snapshots WHERE snapshot_id = '{first}'"));
    }

    [Fact]
    public void SaveSnapshotJson_DefaultsToGuidIdAndIsoUtcTime()
    {
        using var storage = new SqliteStorageManager();
        var id = storage.SaveSnapshotJson("{}");

        var info = Assert.Single(storage.ListSnapshots());

        Assert.True(Guid.TryParse(id, out _));
        Assert.Equal(id, info.SnapshotId);
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse(info.CreatedAt, System.Globalization.CultureInfo.InvariantCulture).Offset);
    }

    [Fact]
    public async Task SaveSnapshotJson_CanRunOnWorkerThreads()
    {
        using var storage = new SqliteStorageManager(_dir.File("s.db"));

        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(n => Task.Run(() => storage.SaveSnapshotJson($"{{\"n\":{n}}}"))));

        Assert.Equal(8, ids.Distinct().Count());
        Assert.Equal(8, storage.ListSnapshots().Count);
    }

    [Theory]
    [InlineData("not json {")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    public void SaveSnapshotJson_NotAJsonObject_Throws(string data)
    {
        using var storage = new SqliteStorageManager();

        Assert.Throws<ArgumentException>(() => storage.SaveSnapshotJson(data));
        Assert.Null(storage.GetLatestSnapshotId());
    }

    [Fact]
    public void SaveSnapshotJson_AboveBlobLimit_Throws()
    {
        using var storage = new SqliteStorageManager(limits: new StorageLimits { MaxBlobBytes = 20 });

        Assert.Throws<ArgumentException>(() => storage.SaveSnapshotJson("{\"a\":\"" + new string('x', 30) + "\"}"));
    }

    [Fact]
    public void LoadSnapshotJson_ReturnsStoredText()
    {
        using var storage = new SqliteStorageManager();
        var id = storage.SaveSnapshotJson("{\"version\":2}");

        Assert.Equal("{\"version\":2}", storage.LoadSnapshotJson(id));
    }

    [Fact]
    public void LoadSnapshotJson_Missing_ThrowsSnapshotNotFound()
    {
        using var storage = new SqliteStorageManager();

        var e = Assert.Throws<SnapshotNotFoundException>(() => storage.LoadSnapshotJson("nope"));

        Assert.Contains("'nope' not found", e.Message);
    }

    [Fact]
    public void LoadSnapshotJson_IdWithSqlText_IsTreatedAsData()
    {
        using var storage = new SqliteStorageManager();
        storage.SaveSnapshotJson("{}", "a");

        Assert.Throws<SnapshotNotFoundException>(() => storage.LoadSnapshotJson("a' OR '1'='1"));
    }

    [Fact]
    public void LoadSnapshotJson_AboveBlobLimit_ThrowsWithoutReadingIt()
    {
        using (var big = new SqliteStorageManager(_dir.File("s.db")))
        {
            big.SaveSnapshotJson("{\"a\":\"" + new string('x', 500) + "\"}", "big");
        }

        using var small = new SqliteStorageManager(_dir.File("s.db"), limits: new StorageLimits { MaxBlobBytes = 100 });

        var e = Assert.Throws<DeserializationException>(() => small.LoadSnapshotJson("big"));
        Assert.Contains("above the limit", e.Message);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[1,2]")]
    public void LoadSnapshotJson_TamperedRow_ThrowsDeserialization(string tampered)
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            storage.SaveSnapshotJson("{}", "s1");
        }

        using (var conn = Raw("s.db"))
        using (var command = conn.CreateCommand())
        {
            command.CommandText = "UPDATE snapshots SET data = @data";
            command.Parameters.AddWithValue("@data", tampered);
            command.ExecuteNonQuery();
        }

        using var reopened = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Throws<DeserializationException>(() => reopened.LoadSnapshotJson("s1"));
    }

    [Fact]
    public void GetLatestSnapshotId_Empty_ReturnsNull()
    {
        using var storage = new SqliteStorageManager();

        Assert.Null(storage.GetLatestSnapshotId());
    }

    [Fact]
    public void GetLatestSnapshotId_ReturnsMostRecent_AndListIsOldestFirst()
    {
        using var storage = new SqliteStorageManager();
        storage.SaveSnapshotJson("{\"value\":1}", "one", "2026-01-01T00:00:00+00:00");
        storage.SaveSnapshotJson("{\"value\":99}", "two", "2026-01-02T00:00:00+00:00");

        Assert.Equal("two", storage.GetLatestSnapshotId());
        Assert.Equal(["one", "two"], storage.ListSnapshots().Select(s => s.SnapshotId));
    }

    [Fact]
    public void Snapshots_SurviveReopen()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            storage.SaveSnapshotJson("{\"counter\":42}", "kept");
        }

        using var reopened = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Equal("{\"counter\":42}", reopened.LoadSnapshotJson("kept"));
    }

    // -- open options --

    [Fact]
    public void MustExist_MissingFile_ThrowsAndCreatesNothing()
    {
        var path = _dir.File("gone.db");

        Assert.Throws<FileNotFoundException>(() => new SqliteStorageManager(path, mustExist: true));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(_dir.File("gone.lock")));
        using var storage = new SqliteStorageManager(path);
        storage.SaveSnapshotJson("{}");
    }

    [Fact]
    public void MustExist_MissingDirectory_Throws()
    {
        var missing = _dir.File("no-such-dir");

        Assert.Throws<FileNotFoundException>(() => new SqliteStorageManager(Path.Combine(missing, "s.db"), mustExist: true));

        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void MustExist_ExistingFile_Opens()
    {
        string first;
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            first = storage.SaveSnapshotJson("{}");
        }

        using var reopened = new SqliteStorageManager(_dir.File("s.db"), mustExist: true);

        Assert.Equal(first, reopened.GetLatestSnapshotId());
    }

    [Fact]
    public void MustExist_WithMemory_Throws() =>
        Assert.Throws<ArgumentException>(() => new SqliteStorageManager(":memory:", mustExist: true));

    [Fact]
    public void JournalMode_DeleteAndWal_AreUsedAsGiven()
    {
        using (var storage = new SqliteStorageManager(_dir.File("d.db"), journalMode: SqliteJournalMode.Delete))
        {
            storage.SaveSnapshotJson("{}");
            Assert.Equal("delete", Scalar("d.db", "PRAGMA journal_mode"));
        }

        using (var storage = new SqliteStorageManager(_dir.File("w.db"), journalMode: SqliteJournalMode.Wal))
        {
            Assert.Equal("wal", Scalar("w.db", "PRAGMA journal_mode"));
        }
    }

    [Fact]
    public void JournalMode_Default_IsWal()
    {
        using var storage = new SqliteStorageManager(_dir.File("n.db"));

        Assert.Equal("wal", Scalar("n.db", "PRAGMA journal_mode"));
    }

    [Fact]
    public void JournalMode_Delete_ConvertsAWalFile()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db"), journalMode: SqliteJournalMode.Wal))
        {
            storage.SaveSnapshotJson("{}");
        }

        using (var storage = new SqliteStorageManager(_dir.File("s.db"), journalMode: SqliteJournalMode.Delete))
        {
            Assert.NotNull(storage.GetLatestSnapshotId());
        }

        Assert.Equal("delete", Scalar("s.db", "PRAGMA journal_mode"));
        Assert.False(File.Exists(_dir.File("s.db-wal")));
    }

    [Fact]
    public void JournalMode_UnknownValue_ThrowsBeforeCreatingFiles()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteStorageManager(_dir.File("x.db"), journalMode: (SqliteJournalMode)7));

        Assert.False(File.Exists(_dir.File("x.db")));
    }

    [Fact]
    public void Open_UsesFullSynchronousAndBusyTimeout()
    {
        using var storage = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Equal("2", Pragma(storage, "synchronous"));
        Assert.Equal("5000", Pragma(storage, "busy_timeout"));
    }

    private static string Pragma(SqliteStorageManager storage, string name)
    {
        using var command = storage.Connection.CreateCommand();
        command.CommandText = "PRAGMA " + name;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    [Fact]
    public void Path_IsNullForMemory_AndFullPathForFile()
    {
        using var memory = new SqliteStorageManager();
        using var file = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Null(memory.DatabasePath);
        Assert.Equal(_dir.File("s.db"), file.DatabasePath);
    }

    [Fact]
    public void Open_PathWithQuestionMark_IsTreatedAsAPath()
    {
        using (var storage = new SqliteStorageManager(_dir.File("a?b.db")))
        {
            storage.SaveSnapshotJson("{}");
        }

        Assert.True(File.Exists(_dir.File("a?b.db")));
    }

    // -- schema --

    [Fact]
    public void Open_CreatesUpstreamTables()
    {
        using (new SqliteStorageManager(_dir.File("s.db")))
        {
        }

        var names = Scalar("s.db", "SELECT group_concat(name) FROM (SELECT name FROM sqlite_master WHERE type IN ('table','index') AND name NOT LIKE 'sqlite_%' ORDER BY name)");

        Assert.Equal(
            "active_tags,events,idx_events_event_id,idx_events_insertion_order,idx_events_type_order,schema_version,snapshots",
            names);
        Assert.Equal("1", Scalar("s.db", "SELECT version FROM schema_version"));
    }

    [Fact]
    public void Open_SchemaVersionMismatch_ThrowsBeforeReadingEvents()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            storage.EventBackend.Store("1", User("a"));
        }

        using (var conn = Raw("s.db"))
        using (var command = conn.CreateCommand())
        {
            command.CommandText = "UPDATE schema_version SET version = 99";
            command.ExecuteNonQuery();
        }

        var e = Assert.Throws<InvalidDataException>(() => new SqliteStorageManager(_dir.File("s.db")));

        Assert.Contains("v99", e.Message);

        // The failed open released the session lock.
        Assert.Null(SqliteStorageManager.ReadLockOwner(_dir.File("s.lock")).Pid);
    }

    [Fact]
    public void Open_NotADatabaseFile_Throws()
    {
        File.WriteAllText(_dir.File("junk.db"), new string('x', 5000));

        var e = Assert.Throws<SqliteException>(() => new SqliteStorageManager(_dir.File("junk.db")));

        Assert.True(SqliteSchema.IsCorruption(e));
    }

    // -- close / lock --

    [Fact]
    public void Dispose_PersistsEvents_AndTwiceIsSafe()
    {
        var storage = new SqliteStorageManager(_dir.File("s.db"));
        storage.EventBackend.Store("1", User("Test event before close"));

        storage.Dispose();
        storage.Dispose();

        using var reopened = new SqliteStorageManager(_dir.File("s.db"));
        Assert.Equal("Test event before close", ((UserEvent)reopened.EventBackend.Get("1")!).Content);
    }

    [Fact]
    public void SessionSwap_PreservesTheOldSession()
    {
        var one = new SqliteStorageManager(_dir.File("one.db"));
        one.EventBackend.Store("1", User("Session 1 message"));
        one.Dispose();
        using (var two = new SqliteStorageManager(_dir.File("two.db")))
        {
            two.EventBackend.Store("1", User("Session 2 message"));
        }

        using var again = new SqliteStorageManager(_dir.File("one.db"));

        Assert.Equal("Session 1 message", ((UserEvent)again.EventBackend.Get("1")!).Content);
    }

    [Fact]
    public void Open_SameFileTwice_ThrowsSessionAlreadyActive()
    {
        using var first = new SqliteStorageManager(_dir.File("s.db"));

        var e = Assert.Throws<SessionAlreadyActiveException>(() => new SqliteStorageManager(_dir.File("s.db")));

        Assert.Equal("s", e.SessionId);
        Assert.Contains("already active", e.Message);
    }

    [Fact]
    public void Open_AfterDispose_Works()
    {
        new SqliteStorageManager(_dir.File("s.db")).Dispose();

        using var again = new SqliteStorageManager(_dir.File("s.db"));

        Assert.NotNull(again);
    }

    [Fact]
    public void Dispose_BlanksTheLockRecord_AndKeepsTheFile()
    {
        new SqliteStorageManager(_dir.File("s.db")).Dispose();

        Assert.True(File.Exists(_dir.File("s.lock")));
        Assert.Equal((null, null), SqliteStorageManager.ReadLockOwner(_dir.File("s.lock")));
    }

    [Fact]
    public void DeleteDatabase_RemovesFiles_AndLeavesABlankLockRecord()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            storage.SaveSnapshotJson("{}");
        }

        Assert.True(SqliteStorageManager.DeleteDatabase(_dir.File("s.db")));

        Assert.False(File.Exists(_dir.File("s.db")));
        Assert.Equal((null, null), SqliteStorageManager.ReadLockOwner(_dir.File("s.lock")));
        Assert.False(SqliteStorageManager.DeleteDatabase(_dir.File("s.db")));
    }

    [Fact]
    public void DeleteDatabase_LiveSession_Throws()
    {
        using var storage = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Throws<SessionAlreadyActiveException>(() => SqliteStorageManager.DeleteDatabase(_dir.File("s.db")));

        Assert.True(File.Exists(_dir.File("s.db")));
    }

    [Fact]
    public void DeleteDatabase_Memory_Throws() =>
        Assert.Throws<ArgumentException>(() => SqliteStorageManager.DeleteDatabase(":memory:"));

    [Fact]
    public void DeleteDatabase_MissingDirectory_ReturnsFalse() =>
        Assert.False(SqliteStorageManager.DeleteDatabase(_dir.File("nope/s.db")));

    // -- corruption --

    [Fact]
    public void Open_OnACorruptFile_ThrowsBeforeUse()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db"), journalMode: SqliteJournalMode.Delete))
        {
            for (var i = 1; i <= 200; i++)
            {
                storage.EventBackend.Store(i.ToString(), User(new string('x', 200)));
            }
        }

        // Overwrite everything after the first two pages (the schema page and the first table page) with garbage.
        using (var stream = new FileStream(_dir.File("s.db"), FileMode.Open, FileAccess.Write))
        {
            stream.Seek(8192, SeekOrigin.Begin);
            stream.Write(Enumerable.Repeat((byte)0xAB, (int)(stream.Length - 8192)).ToArray());
        }

        var e = Record.Exception(() => new SqliteStorageManager(_dir.File("s.db"), journalMode: SqliteJournalMode.Delete));

        Assert.True(e is CorruptDatabaseException || (e is SqliteException se && SqliteSchema.IsCorruption(se)), e?.ToString());
    }

    // -- untrusted file --

    private void CreateSessionFile()
    {
        using var storage = new SqliteStorageManager(_dir.File("s.db"));
        storage.EventBackend.Store("1", User("a"));
    }

    private void ExecRaw(string sql)
    {
        using var conn = Raw("s.db");
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Theory]
    [InlineData("CREATE TRIGGER evil AFTER INSERT ON events BEGIN UPDATE events SET status = 'x'; END")]
    [InlineData("CREATE VIEW v AS SELECT * FROM events")]
    [InlineData("CREATE TABLE extra (a TEXT)")]
    [InlineData("CREATE INDEX idx_extra ON events(status)")]
    public void Open_FileWithAnUnexpectedObject_IsRefused(string ddl)
    {
        CreateSessionFile();
        ExecRaw(ddl);

        var e = Assert.Throws<StorageSchemaException>(() => new SqliteStorageManager(_dir.File("s.db")));

        Assert.Contains("unexpected", e.Message);
        Assert.Equal((null, null), SqliteStorageManager.ReadLockOwner(_dir.File("s.lock")));
    }

    [Fact]
    public void Open_TriggerNamedLikeATable_IsRefused()
    {
        // SQLite keeps trigger names apart from table names, so a trigger can be called "events".
        CreateSessionFile();
        ExecRaw("CREATE TRIGGER events AFTER INSERT ON events BEGIN UPDATE events SET status = 'x'; END");

        Assert.Throws<StorageSchemaException>(() => new SqliteStorageManager(_dir.File("s.db")));
    }

    [Fact]
    public void Open_FileWithAChangedTableDefinition_IsRefused()
    {
        ExecRaw("CREATE TABLE events (tag TEXT PRIMARY KEY, data TEXT GENERATED ALWAYS AS (1) VIRTUAL)");

        Assert.Throws<StorageSchemaException>(() => new SqliteStorageManager(_dir.File("s.db")));
    }

    [Fact]
    public void Open_FileWithOnlySomeExpectedObjects_CompletesTheSchema()
    {
        ExecRaw("CREATE TABLE schema_version (\n    version INTEGER NOT NULL\n)");

        using var storage = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Equal(0, storage.EventBackend.Count);
    }

    [Fact]
    public void Open_TurnsOffTrustedSchema_AndEnablesDefensiveMode()
    {
        using var storage = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Equal("0", Pragma(storage, "trusted_schema"));
        using var command = storage.Connection.CreateCommand();
        command.CommandText = "PRAGMA writable_schema=ON";
        command.ExecuteNonQuery();
        command.CommandText = "UPDATE sqlite_master SET sql = sql";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Open_MoreEventsThanTheLimit_IsRefused()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            for (var i = 1; i <= 3; i++)
            {
                storage.EventBackend.Store(i.ToString(), User("a"));
            }
        }

        var e = Assert.Throws<DeserializationException>(
            () => new SqliteStorageManager(_dir.File("s.db"), limits: new StorageLimits { MaxEventCount = 2 }));

        Assert.Contains("more than 2 events", e.Message);
    }

    [Fact]
    public void Open_MoreEventDataThanTheLimit_IsRefused()
    {
        using (var storage = new SqliteStorageManager(_dir.File("s.db")))
        {
            storage.EventBackend.Store("1", User(new string('x', 500)));
            storage.EventBackend.Store("2", User(new string('x', 500)));
        }

        var e = Assert.Throws<DeserializationException>(
            () => new SqliteStorageManager(_dir.File("s.db"), limits: new StorageLimits { MaxTotalEventBytes = 1000 }));

        Assert.Contains("above the limit", e.Message);
    }

    [Fact]
    public void AllEvents_StopsAtTheLimitsWhileReading()
    {
        using var storage = new SqliteStorageManager(limits: new StorageLimits { MaxEventCount = 2 });
        for (var i = 1; i <= 3; i++)
        {
            storage.EventBackend.Store(i.ToString(), User("a"));
        }

        var seen = new List<EventBase>();
        Assert.Throws<DeserializationException>(() =>
        {
            foreach (var e in storage.EventBackend.AllEvents())
            {
                seen.Add(e);
            }
        });
        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void AllEvents_ReadsManyPagesInOrder()
    {
        using var storage = new SqliteStorageManager();
        for (var i = 1; i <= 250; i++)
        {
            storage.EventBackend.Store(i.ToString(), User("e" + i));
        }

        var contents = storage.EventBackend.AllEvents().Select(e => ((UserEvent)e).Content).ToList();

        Assert.Equal(Enumerable.Range(1, 250).Select(i => "e" + i), contents);
    }

    [Fact]
    public void AllEvents_PageByteBudget_StillReturnsEveryRow()
    {
        using var storage = new SqliteStorageManager();
        var big = new string('z', 3 * 1024 * 1024);
        for (var i = 1; i <= 12; i++)
        {
            storage.EventBackend.Store(i.ToString(), User(big + i));
        }

        Assert.Equal(12, storage.EventBackend.AllEvents().Count());
    }
}
