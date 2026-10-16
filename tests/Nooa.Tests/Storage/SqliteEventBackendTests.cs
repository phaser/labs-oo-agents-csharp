// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_event_backend_protocol.py, tests/test_sqlite_corruption_resilience.py, tests/test_sqlite_specific.py, tests/test_session_resume_tag_collision.py

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Json;
using Nooa.Runtime;
using Nooa.Storage;
using Nooa.Tests.Runtime;

namespace Nooa.Tests.Storage;

public class SqliteEventBackendTests : IEventBackendTests
{
    private readonly TempDirectory _dir = new();
    private readonly CapturingLogger _logger = new();
    private readonly SqliteStorageManager _storage;
    private readonly SqliteEventBackend _backend;

    public SqliteEventBackendTests()
    {
        _storage = new SqliteStorageManager(_dir.File("s.db"), logger: _logger);
        _backend = (SqliteEventBackend)_storage.EventBackend;
    }

    protected override IEventBackend Backend => _backend;

    public override void Dispose()
    {
        _storage.Dispose();
        _dir.Dispose();
        base.Dispose();
    }

    private static UserEvent User(string content) => new() { Content = content };

    // A second connection to the same file, to change rows behind the backend.
    private void Exec(string sql, params (string Name, object Value)[] args)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dir.File("s.db"), Pooling = false }.ToString());
        conn.Open();
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private void InsertRaw(string tag, string eventType, string data) =>
        Exec(
            "INSERT INTO events (tag, event_id, event_type, status, data, insertion_order) VALUES (@tag, @id, @type, 'active', @data, 0)",
            ("@tag", tag),
            ("@id", "id-" + tag),
            ("@type", eventType),
            ("@data", data));

    [Fact]
    public void Store_WritesDenormalizedColumns()
    {
        var e = User("x");
        _backend.Store("1", e);
        _backend.SetStatus("1", EventStatus.Archived);

        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dir.File("s.db"), Pooling = false }.ToString());
        conn.Open();
        using var command = conn.CreateCommand();
        command.CommandText = "SELECT event_id, event_type, status FROM events WHERE tag = '1'";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal((e.Id, "UserEvent", "archived"), (reader.GetString(0), reader.GetString(1), reader.GetString(2)));
    }

    [Fact]
    public void Get_TagWithSqlText_IsTreatedAsData()
    {
        _backend.Store("1", User("safe"));

        Assert.Null(_backend.Get("1' OR '1'='1"));
        Assert.Equal(1, _backend.Count);
    }

    [Fact]
    public void Get_InvalidJson_ReturnsNullAndLogs()
    {
        _backend.Store("1", User("a"));
        Exec("UPDATE events SET data = '{not valid json' WHERE tag = '1'");

        Assert.Null(_backend.Get("1"));
        Assert.Contains("corrupt event data", _logger.Text);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"event_type\": 5}")]
    public void Get_JsonThatIsNotAnEvent_ReturnsNull(string payload)
    {
        _backend.Store("1", User("a"));
        Exec("UPDATE events SET data = @data WHERE tag = '1'", ("@data", payload));

        Assert.Null(_backend.Get("1"));
    }

    [Fact]
    public void Get_KnownTypeWithMissingField_ReturnsNull()
    {
        _backend.Store("1", User("a"));
        Exec("UPDATE events SET data = '{\"event_type\":\"UserEvent\"}' WHERE tag = '1'");

        Assert.Null(_backend.Get("1"));
    }

    [Fact]
    public void GetById_CorruptData_ReturnsNull()
    {
        var e = User("a");
        _backend.Store("1", e);
        Exec("UPDATE events SET data = '{not valid json' WHERE tag = '1'");

        Assert.Null(_backend.GetById(e.Id));
    }

    [Theory]
    [InlineData("zeroblob(4096)")]
    [InlineData("CAST(X'80808080' AS TEXT)")]
    [InlineData("CAST(X'FF' AS TEXT)")]
    public void Get_BinaryOrBadUtf8Data_ReturnsNull(string sqlValue)
    {
        _backend.Store("1", User("a"));
        Exec($"UPDATE events SET data = {sqlValue} WHERE tag = '1'");

        Assert.Null(_backend.Get("1"));
    }

    [Fact]
    public void AllEvents_SkipsCorruptRows()
    {
        for (var i = 1; i <= 3; i++)
        {
            _backend.Store(i.ToString(), User("e" + i));
        }

        Exec("UPDATE events SET data = '{broken}' WHERE tag = '2'");

        Assert.Equal(2, _backend.AllEvents().Count());
    }

    [Fact]
    public void AllEvents_AllRowsCorrupt_ReturnsNothing()
    {
        _backend.Store("1", User("a"));
        _backend.Store("2", User("b"));
        Exec("UPDATE events SET data = zeroblob(100)");

        Assert.Empty(_backend.AllEvents());
    }

    [Fact]
    public void Session_SurvivesOneCorruptEvent()
    {
        for (var i = 1; i <= 5; i++)
        {
            _backend.Store(i.ToString(), User("e" + i));
        }

        Exec("UPDATE events SET data = CAST(X'FF' AS TEXT) WHERE tag = '3'");

        Assert.Null(_backend.Get("3"));
        Assert.All(new[] { "1", "2", "4", "5" }, tag => Assert.NotNull(_backend.Get(tag)));
    }

    [Fact]
    public void Get_UnknownEventType_ThrowsDeserializationException()
    {
        InsertRaw("1", "totally_unknown_type", "{\"event_type\":\"totally_unknown_type\",\"id\":\"abc\"}");

        var e = Assert.Throws<DeserializationException>(() => _backend.Get("1"));

        Assert.Contains("totally_unknown_type", e.Message);
    }

    [Fact]
    public void AllEvents_UnknownEventType_Throws()
    {
        _backend.Store("1", User("a"));
        InsertRaw("2", "mystery_event", "{\"event_type\":\"mystery_event\"}");

        Assert.Throws<DeserializationException>(() => _backend.AllEvents().ToList());
    }

    [Fact]
    public void Get_TypeNameFromAnAssembly_IsRefused()
    {
        InsertRaw("1", "System.Diagnostics.Process, System", "{\"event_type\":\"System.Diagnostics.Process, System\",\"$type\":\"System.Diagnostics.Process, System\"}");

        Assert.Throws<DeserializationException>(() => _backend.Get("1"));
    }

    [Fact]
    public void Get_RowAboveBlobLimit_Throws()
    {
        using var small = new SqliteStorageManager(_dir.File("small.db"), maxBlobBytes: 400);
        small.EventBackend.Store("1", User(new string('x', 100)));
        small.EventBackend.Store("2", User(new string('y', 600)));

        Assert.NotNull(small.EventBackend.Get("1"));
        var e = Assert.Throws<DeserializationException>(() => small.EventBackend.Get("2"));
        Assert.Contains("above the limit", e.Message);
    }

    [Fact]
    public void Update_OnCorruptRow_ReturnsFalse()
    {
        _backend.Store("1", User("a"));
        Exec("UPDATE events SET data = '{broken}' WHERE tag = '1'");

        Assert.False(_backend.Update("1", _ => { }));
    }

    [Fact]
    public void Store_DuplicateTag_Throws()
    {
        _backend.Store("1", User("a"));

        Assert.Throws<SqliteException>(() => _backend.Store("1", User("b")));
        Assert.Equal(1, _backend.Count);
    }

    [Fact]
    public void MaxTagNum_MultiDigitRangeTags()
    {
        InsertRaw("12..234", "x", "{}");
        InsertRaw("233..293432", "x", "{}");
        InsertRaw("500", "x", "{}");

        Assert.Equal(293432, _backend.MaxTagNum());
    }

    [Fact]
    public void MaxTagNum_Empty_IsZero() => Assert.Equal(0, _backend.MaxTagNum());

    [Fact]
    public void MaxTagNum_SimpleTags()
    {
        InsertRaw("1", "x", "{}");
        InsertRaw("99", "x", "{}");
        InsertRaw("42", "x", "{}");

        Assert.Equal(99, _backend.MaxTagNum());
    }

    [Fact]
    public void Reopen_StartsTagCounterAboveStoredTags_AndKeepsOrder()
    {
        _backend.Store("1", User("a"));
        _backend.Store("2..5", User("range"));
        _backend.Store("6", User("b"));
        _storage.Dispose();

        using var reopened = new SqliteStorageManager(_dir.File("s.db"));

        Assert.Equal("7", reopened.EventBackend.AllocateNextTag());
        reopened.EventBackend.Store("7", User("c"));
        Assert.Equal(["a", "range", "b", "c"], reopened.EventBackend.AllEvents().Select(e => ((UserEvent)e).Content));
    }

    [Fact]
    public void TwoBackends_SameStorage_ShareTheTagCounter()
    {
        var tags = new[] { _backend.AllocateNextTag(), _backend.AllocateNextTag(), _backend.AllocateNextTag() };

        Assert.Equal(["1", "2", "3"], tags);
    }

    [Fact]
    public void Serialized_UsesNooaJsonThroughEventBase()
    {
        var e = User("wire");
        _backend.Store("1", e);

        Assert.Equal(EventJson.Write(e), JsonSerializer.Serialize<EventBase>(_backend.Get("1")!, NooaJson.Options));
    }
}
