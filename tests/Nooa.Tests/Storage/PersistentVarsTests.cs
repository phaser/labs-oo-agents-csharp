// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/tests/test_todo_vars.py, tests/unit/test_todo_status.py (persistent vars part)

using System.Text.Json.Nodes;
using Nooa.Storage;
using Nooa.Tools;

namespace Nooa.Tests.Storage;

public class PersistentVarsTests
{
    [Fact]
    public void SetAndGet_StoresValueInTheSharedDictionary()
    {
        var todo = new Todo { Title = "test" };

        Assert.True(todo.V.Set("x", 42));

        Assert.Equal(42, todo.V.Get<int>("x"));
        Assert.Equal(42, todo.V["x"]!.GetValue<int>());
        Assert.Equal(42, todo.Vars["x"]!.GetValue<int>());
    }

    [Fact]
    public void Indexer_MissingName_ThrowsWithTheName()
    {
        var error = Assert.Throws<KeyNotFoundException>(() => new Todo().V["nope"]);

        Assert.Contains("No var 'nope'", error.Message);
    }

    [Fact]
    public void Get_MissingName_ReturnsDefault()
    {
        var vars = new Todo().V;

        Assert.Equal(7, vars.Get("nope", 7));
        Assert.Null(vars.Get<string>("nope"));
    }

    [Fact]
    public void Get_StoredNull_ReturnsNull()
    {
        var vars = new Todo().V;
        vars.Set("n", null);

        Assert.Null(vars.Get<string>("n", "fallback"));
    }

    [Fact]
    public void Remove_RemovesAndMissingThrows()
    {
        var todo = new Todo();
        todo.V.Set("x", 1);

        todo.V.Remove("x");

        Assert.DoesNotContain("x", todo.Vars.Keys);
        var error = Assert.Throws<KeyNotFoundException>(() => todo.V.Remove("x"));
        Assert.Contains("No var 'x'", error.Message);
    }

    [Fact]
    public void Contains_ReflectsStoredNames()
    {
        var todo = new Todo();
        Assert.False(todo.V.Contains("x"));

        todo.V.Set("x", 1);

        Assert.True(todo.V.Contains("x"));
    }

    [Fact]
    public void ToString_IsTheJsonObject()
    {
        var todo = new Todo();
        todo.V.Set("a", 1);

        Assert.Equal("{\"a\":1}", todo.V.ToString());
    }

    [Fact]
    public void V_SharedWithVarsDictionary()
    {
        var todo = new Todo();
        todo.Vars["viaDict"] = JsonValue.Create("hello");

        Assert.Equal("hello", todo.V.Get<string>("viaDict"));
        todo.V.Set("viaProxy", "world");
        Assert.Equal("world", todo.Vars["viaProxy"]!.GetValue<string>());
    }

    [Fact]
    public void V_SharedWithTodoManagerVars()
    {
        var manager = new TodoManager();
        var todo = manager.Add("test");
        manager.SetVar(todo, "key", new List<int> { 1, 2, 3 });

        Assert.Equal([1, 2, 3], todo.V.Get<List<int>>("key"));
        todo.V.Set("other", "val");
        Assert.Equal("val", manager.GetVar(todo, "other")!.GetValue<string>());
    }

    [Fact]
    public void V_SurvivesSerialization()
    {
        var manager = new TodoManager();
        var todo = manager.Add("test");
        todo.V.Set("data", new Dictionary<string, object?> { ["nested"] = true });

        var restored = new TodoManager(manager.ToJson());

        Assert.True(restored.Get(todo)!.V.Get<Dictionary<string, bool>>("data")!["nested"]);
        Assert.DoesNotContain("\"v\"", manager.ToJson().ToJsonString());
    }

    [Fact]
    public void V_EachAccess_SharesTheSameState()
    {
        var todo = new Todo();
        var first = todo.V;
        var second = todo.V;

        first.Set("x", 99);

        Assert.Equal(99, second.Get<int>("x"));
    }

    [Fact]
    public void Set_ValueWithoutJsonForm_IsNotStored()
    {
        var vars = new Todo().V;

        Assert.False(vars.Set("fn", new Action(() => { })));
        Assert.False(vars.Set("bad", new Dictionary<int, int> { [1] = 1 }));

        Assert.Equal(0, vars.Count);
    }

    [Fact]
    public void KeysAndItems_ListTheStoredValues()
    {
        var vars = new Todo().V;
        vars.Set("a", 1);
        vars.Set("b", "two");

        Assert.Equal(["a", "b"], vars.Keys());
        Assert.Equal(["a", "b"], vars.Items().Select(p => p.Key));
        vars.Clear();
        Assert.Equal(0, vars.Count);
    }

    [Fact]
    public void Constructor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PersistentVars(null!));
    }
}
