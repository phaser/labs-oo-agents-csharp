// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unit/test_todo_comments.py, src/nooa/tools/tests/test_todo_vars.py (vars part), tests/unit/test_todo_status.py (snapshot and delegation part)

using System.Text.Json.Nodes;
using Nooa.Errors;
using Nooa.Json;
using Nooa.Llm;
using Nooa.Storage;
using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class TodoManagerTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Comment_Existing_ReturnsStoredInstance()
    {
        var tm = new TodoManager();
        var t = tm.Add("Fix bug");

        var c = tm.Comment(t.Id, "root cause: races on refresh");

        Assert.NotNull(c);
        Assert.Equal("root cause: races on refresh", c.Body);
        Assert.NotEmpty(c.CreatedAt);
    }

    [Fact]
    public void Comment_Missing_ReturnsNull() => Assert.Null(new TodoManager().Comment("does-not-exist", "hello"));

    [Fact]
    public void Comments_NewTodo_IsEmpty()
    {
        var tm = new TodoManager();

        Assert.Empty(tm.Comments(tm.Add("New").Id));
        Assert.Empty(tm.Comments("nope"));
    }

    [Fact]
    public void Comments_AreChronologicalAndAppendOnly()
    {
        var tm = new TodoManager();
        var t = tm.Add("Multi-step work");
        tm.Comment(t.Id, "first");
        tm.Comment(t.Id, "second");
        tm.Comment(t.Id, "third");

        Assert.Equal(["first", "second", "third"], tm.Comments(t.Id).Select(c => c.Body));
    }

    [Fact]
    public void Comments_SurviveSnapshotRoundTrip()
    {
        var tm = new TodoManager();
        var t = tm.Add("With history");
        tm.Comment(t.Id, "one");
        tm.Comment(t.Id, "two");

        var restored = new TodoManager();
        restored.Restore(tm.ToJson());

        Assert.Equal(["one", "two"], restored.Comments(t.Id).Select(c => c.Body));
    }

    [Fact]
    public void Comment_DoesNotOverwriteDescription()
    {
        var tm = new TodoManager();
        var t = tm.Add("Item", description: "static note");
        tm.Comment(t.Id, "progress log entry");

        Assert.Equal("static note", t.Description);
        Assert.Equal(["progress log entry"], tm.Comments(t.Id).Select(c => c.Body));
    }

    [Fact]
    public void Comments_MultipleTodos_StaySeparate()
    {
        var tm = new TodoManager();
        var a = tm.Add("A");
        var b = tm.Add("B");
        tm.Comment(a, "about A");
        tm.Comment(b, "about B");

        Assert.Equal(["about A"], tm.Comments(a).Select(c => c.Body));
        Assert.Equal(["about B"], tm.Comments(b).Select(c => c.Body));
    }

    [Theory]
    [InlineData("")]
    [InlineData("just some text")]
    [InlineData("multi\nline\nbody")]
    [InlineData("🔍 emoji")]
    public void Comment_ArbitraryBody_IsStored(string body)
    {
        var tm = new TodoManager();

        Assert.Equal(body, tm.Comment(tm.Add("T"), body)!.Body);
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var tm = new TodoManager();
        tm.Add("first");
        tm.Add("second");

        tm.Clear();

        Assert.Empty(tm.ListTodos());
    }

    [Fact]
    public void Clear_ThenAdd_StartsFresh()
    {
        var tm = new TodoManager();
        var old = tm.Add("old");
        tm.Clear();

        var added = tm.Add("new");

        Assert.NotEqual(old.Id, added.Id);
        Assert.Equal([added], tm.ListTodos());
    }

    [Fact]
    public void Methods_AcceptTodoObjectsAndIds()
    {
        var tm = new TodoManager();
        var first = tm.Add("first");
        var second = tm.Add("second", deps: [first]);

        Assert.Same(first, tm.Get(first));
        Assert.Same(first, tm.Get(first.Id));
        Assert.Equal([first.Id], second.Deps);
        Assert.Same(first, tm.AddDep(first, second));
        Assert.Equal([second.Id], first.Deps);
        Assert.Same(first, tm.RemoveDep(first, second));
        Assert.Empty(first.Deps);
        Assert.Same(first, tm.Update(first, title: "renamed", description: "detail"));
        Assert.Equal(("renamed", "detail"), (first.Title, first.Description));
        Assert.Same(first, tm.SetVar(first, "answer", 42));
        Assert.Equal(42, (int)tm.GetVar(first, "answer")!);
        Assert.Same(first, tm.DelVar(first, "answer"));
        Assert.Null(tm.GetVar(first, "answer"));
        Assert.NotNull(tm.Comment(first, "finding"));
        Assert.Equal(["finding"], tm.Comments(first).Select(c => c.Body));
        Assert.Same(first, tm.Done(first));
        Assert.Equal(TodoStatus.Done, first.Status);
        Assert.Same(first, tm.Reopen(first));
        Assert.Equal(TodoStatus.Open, first.Status);
        Assert.True(tm.Remove(second));
        Assert.Null(tm.Get(second));
    }

    [Fact]
    public void Add_StoresJsonVarsAndDropsUnserializableOnes()
    {
        var tm = new TodoManager();

        var t = tm.Add("x", vars: new Dictionary<string, object?> { ["good"] = 1, ["bad"] = new object(), ["nested"] = new Dictionary<string, object?> { ["a"] = new List<int> { 1 } } });

        Assert.Equal(1, (int)t.Vars["good"]!);
        Assert.DoesNotContain("bad", t.Vars.Keys);
        Assert.Equal(1, (int)t.Vars["nested"]!["a"]![0]!);
    }

    [Fact]
    public void SetVar_Unserializable_IsNotStored()
    {
        var tm = new TodoManager();
        var t = tm.Add("x");

        tm.SetVar(t, "bad", new object());

        Assert.Empty(t.Vars);
    }

    [Fact]
    public void Vars_SurviveSnapshotRoundTrip()
    {
        var tm = new TodoManager();
        var t = tm.Add("test");
        tm.SetVar(t, "data", new Dictionary<string, object?> { ["nested"] = true });

        var restored = new TodoManager(tm.ToJson());

        Assert.True((bool)restored.GetVar(t.Id, "data")!["nested"]!);
    }

    [Fact]
    public void Add_DepsBlockUntilDone()
    {
        var tm = new TodoManager();
        var dep = tm.Add("dep");

        var t = tm.Add("t", deps: [dep]);

        Assert.True(t.IsBlocked(tm.ListTodos().ToDictionary(x => x.Id)));
        tm.Complete(dep);
        Assert.False(t.IsBlocked(tm.ListTodos().ToDictionary(x => x.Id)));
    }

    [Fact]
    public void Restore_LegacyNotesAndStatus_AreMigrated()
    {
        var restored = new TodoManager(Obj("""
            {"todos":[{"id":"a","title":"legacy","status":"in_progress","deps":[],"vars":{},"created_at":"2026-01-01 00:00","notes":"legacy snapshot","comments":[{"body":"c"}]}]}
            """));

        var todo = restored.Get("a")!;

        Assert.Equal("legacy snapshot", todo.Description);
        Assert.Equal(TodoStatus.Open, todo.Status);
        Assert.NotEmpty(todo.Comments[0].Id);
        var json = restored.ToJson();
        Assert.Null(json["todos"]![0]!["notes"]);
        Assert.Equal("legacy snapshot", (string)json["todos"]![0]!["description"]!);
    }

    [Theory]
    [InlineData("open", TodoStatus.Open)]
    [InlineData("blocked", TodoStatus.Open)]
    [InlineData("in_progress", TodoStatus.Open)]
    [InlineData("custom", TodoStatus.Open)]
    [InlineData(null, TodoStatus.Open)]
    [InlineData("done", TodoStatus.Done)]
    public void Restore_LegacyStatus_BecomesOpenUnlessDone(string? status, TodoStatus expected)
    {
        var tm = new TodoManager();
        var todo = tm.Add("old snapshot");
        var state = tm.ToJson();
        state["todos"]![0]!["status"] = status;

        tm.Restore(state);

        Assert.Equal(expected, tm.Get(todo.Id)!.Status);
    }

    [Fact]
    public void Restore_BadTodo_KeepsExistingWorkspace()
    {
        var tm = new TodoManager();
        tm.Add("original");
        var before = tm.ToJson();
        var broken = Obj(before.ToJsonString());
        ((JsonArray)broken["todos"]!).Add(Obj("""{"title":42}"""));

        Assert.Throws<DeserializationException>(() => tm.Restore(broken));

        Assert.True(JsonNode.DeepEquals(before, tm.ToJson()));
    }

    [Fact]
    public void Restore_DuplicateIds_Throws()
    {
        var state = Obj("""{"todos":[{"id":"a"},{"id":"a"}]}""");

        Assert.Throws<DeserializationException>(() => new TodoManager(state));
    }

    [Fact]
    public void Active_RoundTripsAndIgnoresInvalidIds()
    {
        var tm = new TodoManager();
        var root = tm.Add("root");
        tm.Activate(root);

        Assert.Equal(root.Id, new TodoManager(tm.ToJson()).Active()!.Id);

        var stale = tm.ToJson();
        stale["active_id"] = "missing";
        Assert.Null(new TodoManager(stale).Active());

        root.Status = TodoStatus.Done;
        Assert.Null(tm.ToJson()["active_id"]);
    }

    [Fact]
    public void WithJsonOptions_RoundTripsThroughConverter()
    {
        var tm = new TodoManager();
        var t = tm.Add("a", description: "d");
        tm.Activate(t);

        var json = System.Text.Json.JsonSerializer.Serialize(tm, NooaJson.Options);
        var back = System.Text.Json.JsonSerializer.Deserialize<TodoManager>(json, NooaJson.Options)!;

        Assert.Equal("d", back.Get(t.Id)!.Description);
        Assert.Equal(t.Id, back.Active()!.Id);
    }

    [Fact]
    public void AgentSnapshot_RestoresTodoManagerField()
    {
        var source = new TodoAgent(new AgentOptions { Llm = new FakeLlmClient() });
        var task = source.Todo.Add("one", vars: new Dictionary<string, object?> { ["k"] = "v" });
        source.Todo.Comment(task, "note");
        source.Todo.Activate(task);
        var target = new TodoAgent(new AgentOptions { Llm = new FakeLlmClient() });

        SnapshotJson.FromJson(SnapshotJson.ToJson(source), target);

        var restored = target.Todo.Get(task.Id)!;
        Assert.Equal("v", (string)restored.Vars["k"]!);
        Assert.Equal(["note"], restored.Comments.Select(c => c.Body));
        Assert.Equal(task.Id, target.Todo.Active()!.Id);
    }

    [Fact]
    public void AgentSnapshot_ForgedTodoManagerState_IsRefusedWithoutChange()
    {
        var source = new TodoAgent(new AgentOptions { Llm = new FakeLlmClient() });
        source.Todo.Add("one");
        var forged = JsonNode.Parse(SnapshotJson.ToJson(source))!;
        var todos = (JsonArray)forged["attributes"]!["Todo"]!["todos"]!;
        todos.Add(JsonNode.Parse("""{"title":42}"""));
        var target = new TodoAgent(new AgentOptions { Llm = new FakeLlmClient() });
        target.Todo.Add("keep");

        Assert.ThrowsAny<Exception>(() => SnapshotJson.FromJson(forged.ToJsonString(), target));

        Assert.Equal(["keep"], target.Todo.ListTodos().Select(t => t.Title));
    }

    [Fact]
    public void WithTodo_OpenTodo_IsActive()
    {
        var todo = new TodoManager().Add("delegated");

        var worker = TodoManager.WithTodo(todo);

        Assert.Equal(todo.Id, worker.Active()!.Id);
    }

    [Fact]
    public void CopyTodo_Unmanaged_Throws() =>
        Assert.Throws<ValidationException>(() => new TodoManager().CopyTodo("nope"));

    [Fact]
    public void MergeTodo_CopyIsIndependentAndMergePreservesIdentity()
    {
        var tm = new TodoManager();
        var original = tm.Add("review", description: "start", vars: new Dictionary<string, object?> { ["nested"] = new Dictionary<string, object?> { ["values"] = new List<int> { 1 } } });
        tm.Comment(original, "controller baseline");
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();

        worker.Description = "worker description";
        ((JsonArray)worker.Vars["nested"]!["values"]!).Add(2);
        worker.SetVar("result", new Dictionary<string, object?> { ["file"] = "parser.py" });
        worker.Comments.Add(new TodoComment { Body = "worker finding" });
        var merged = tm.MergeTodo(worker, baseTodo);

        Assert.Same(original, merged);
        Assert.Equal("worker description", original.Description);
        Assert.Equal("""{"values":[1,2]}""", original.Vars["nested"]!.ToJsonString());
        Assert.Equal("parser.py", (string)original.Vars["result"]!["file"]!);
        Assert.Equal(["controller baseline", "worker finding"], original.Comments.Select(c => c.Body));
        Assert.Equal("""{"values":[1]}""", baseTodo.Vars["nested"]!.ToJsonString());
    }

    [Fact]
    public void MergeTodo_KeepsUnrelatedControllerChanges()
    {
        var tm = new TodoManager();
        var original = tm.Add("review", vars: new Dictionary<string, object?> { ["controller"] = "initial" });
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();

        original.SetVar("controller", "new");
        worker.SetVar("worker", "finding");
        tm.Comment(original, "controller note");
        worker.Comments.Add(new TodoComment { Body = "worker note" });
        tm.MergeTodo(worker, baseTodo);

        Assert.Equal("new", (string)original.Vars["controller"]!);
        Assert.Equal("finding", (string)original.Vars["worker"]!);
        Assert.Equal(["controller note", "worker note"], original.Comments.Select(c => c.Body));
    }

    [Fact]
    public void MergeTodo_RemovedVar_IsRemoved()
    {
        var tm = new TodoManager();
        var original = tm.Add("review", vars: new Dictionary<string, object?> { ["a"] = 1 });
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();
        worker.Vars.Remove("a");

        tm.MergeTodo(worker, baseTodo);

        Assert.Empty(original.Vars);
    }

    [Fact]
    public void MergeTodo_ConflictingField_Throws()
    {
        var tm = new TodoManager();
        var original = tm.Add("review");
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();
        original.Description = "controller description";
        worker.Description = "worker description";

        var e = Assert.Throws<ValidationException>(() => tm.MergeTodo(worker, baseTodo));

        Assert.Contains("conflicting 'description' changes", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("variable")]
    [InlineData("worker_comment")]
    [InlineData("parent_comment")]
    public void MergeTodo_Conflict_NeverPartiallyCommits(string conflict)
    {
        var tm = new TodoManager();
        var original = tm.Add("review", vars: new Dictionary<string, object?> { ["shared"] = "baseline" });
        tm.Comment(original, "original comment");
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();
        worker.Title = "must not commit";
        switch (conflict)
        {
            case "variable":
                original.SetVar("shared", "parent");
                worker.SetVar("shared", "worker");
                break;
            case "worker_comment":
                worker.Comments[0].Body = "edited by worker";
                break;
            default:
                original.Comments[0].Body = "edited by parent";
                break;
        }

        var before = tm.ToJson();

        var e = Assert.Throws<ValidationException>(() => tm.MergeTodo(worker, baseTodo));

        Assert.Matches("conflicting|modified existing", e.Message);
        Assert.True(JsonNode.DeepEquals(before, tm.ToJson()));
    }

    [Fact]
    public void MergeTodo_ConflictIsAtomic()
    {
        var tm = new TodoManager();
        var original = tm.Add("review", description: "base");
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();
        worker.Title = "worker title";
        worker.Description = "worker description";
        worker.SetVar("finding", "worker value");
        worker.Comments.Add(new TodoComment { Body = "worker comment" });
        original.Description = "controller description";

        Assert.Throws<ValidationException>(() => tm.MergeTodo(worker, baseTodo));

        Assert.Equal("review", original.Title);
        Assert.Equal("controller description", original.Description);
        Assert.DoesNotContain("finding", original.Vars.Keys);
        Assert.Empty(original.Comments);
    }

    [Fact]
    public void MergeTodo_EqualConcurrentComments_AreBothKept()
    {
        var tm = new TodoManager();
        var original = tm.Add("review");
        var baseTodo = tm.CopyTodo(original);
        var worker = baseTodo.Copy();
        var controllerComment = new TodoComment { Body = "same", CreatedAt = "2026-01-01 00:00" };
        var workerComment = new TodoComment { Body = "same", CreatedAt = "2026-01-01 00:00" };
        original.Comments.Add(controllerComment);
        worker.Comments.Add(workerComment);

        tm.MergeTodo(worker, baseTodo);

        Assert.Equal([controllerComment.Id, workerComment.Id], original.Comments.Select(c => c.Id));
    }

    [Fact]
    public void MergeTodo_WorkerLocalDependency_IsRefused()
    {
        var tm = new TodoManager();
        var task = tm.Add("work");
        var baseTodo = tm.CopyTodo(task);
        var worker = TodoManager.WithTodo(baseTodo);
        var dependency = worker.Add("worker-only");
        worker.AddDep(task, dependency);
        var before = tm.ToJson();

        var e = Assert.Throws<ValidationException>(() => tm.MergeTodo(worker.Get(task)!, baseTodo));

        Assert.Contains("dependencies are not in the parent", e.Message, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(before, tm.ToJson()));
    }

    [Fact]
    public void MergeTodo_Done_ClearsActive()
    {
        var tm = new TodoManager();
        var merged = tm.Add("merged");
        tm.Activate(merged);
        var baseTodo = tm.CopyTodo(merged);
        var worker = baseTodo.Copy();
        worker.Status = TodoStatus.Done;

        tm.MergeTodo(worker, baseTodo);

        Assert.Null(tm.Active());
    }

    [Fact]
    public void MergeTodo_DifferentIds_Throws()
    {
        var tm = new TodoManager();
        var a = tm.Add("a");
        var b = tm.Add("b");

        Assert.Throws<ValidationException>(() => tm.MergeTodo(a, b));
    }

    private sealed class TodoAgent(AgentOptions options) : Agent(options)
    {
#pragma warning disable CA1051 // Fixture field is read and written by reflection.
        public TodoManager Todo = new();
#pragma warning restore CA1051
    }
}
