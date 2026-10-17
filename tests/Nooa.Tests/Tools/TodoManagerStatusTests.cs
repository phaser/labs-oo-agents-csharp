// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unit/test_todo_status.py

using Nooa.Agentdoc;
using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class TodoManagerStatusTests
{
    private static List<string> Rows(string output) =>
        [.. output.Split('\n').Where(l => l.StartsWith("  ", StringComparison.Ordinal) && l.Contains('[', StringComparison.Ordinal))];

    private static string RowId(string row) => row.Split('[', 2)[1].Split(']', 2)[0];

    [Fact]
    public void Status_Empty_IsMinimal() => Assert.Equal("(no todos)", new TodoManager().Status());

    [Fact]
    public void Status_HidesPayloadAndAdvertisesInspection()
    {
        var m = new TodoManager();
        var todo = m.Add("Investigate\nmultiline failure", description: "secret description payload", vars: new Dictionary<string, object?> { ["secret"] = new Dictionary<string, object?> { ["large"] = "value" } });
        m.Comment(todo, "secret comment payload");

        var output = m.Status();

        Assert.Contains("Investigate multiline failure", output, StringComparison.Ordinal);
        Assert.Contains("· description · 1 var · 1 comment", output, StringComparison.Ordinal);
        Assert.DoesNotContain("secret description payload", output, StringComparison.Ordinal);
        Assert.DoesNotContain("secret comment payload", output, StringComparison.Ordinal);
        Assert.DoesNotContain("large", output, StringComparison.Ordinal);
        Assert.Contains("Hint: inspect: self.todo.get(id)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("list all", output, StringComparison.Ordinal);
        Assert.DoesNotContain("prune done", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_PrioritizesOpenWorkAndRecentDone()
    {
        var m = new TodoManager();
        m.Done(m.Add("old done"));
        m.Add("first open");
        var blocker = m.Add("blocker");
        m.Add("blocked", deps: [blocker]);
        m.Done(m.Add("recent done"));

        var output = m.Status(maxItems: 3);

        Assert.Contains("Todos (2/5 done; showing 3):", output, StringComparison.Ordinal);
        Assert.True(output.IndexOf("first open", StringComparison.Ordinal) < output.IndexOf("blocker", StringComparison.Ordinal));
        Assert.True(output.IndexOf("blocker", StringComparison.Ordinal) < output.IndexOf("blocked", StringComparison.Ordinal));
        Assert.DoesNotContain("old done", output, StringComparison.Ordinal);
        Assert.DoesNotContain("recent done", output, StringComparison.Ordinal);
        Assert.Contains("… +2 not shown (2 done)", output, StringComparison.Ordinal);
        Assert.Contains("Hint: list all: self.todo.list_todos()", output, StringComparison.Ordinal);
        Assert.Contains("prune done: self.todo.clear_done()", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_UsesRecentDoneWhenCapacityRemains()
    {
        var m = new TodoManager();
        var oldest = m.Add("oldest");
        var newest = m.Add("newest");
        m.Done(oldest);
        m.Done(newest);

        var output = m.Status(maxItems: 1);

        Assert.Contains("newest", output, StringComparison.Ordinal);
        Assert.DoesNotContain("oldest", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_AtExactLimit_IsNotTruncated()
    {
        var m = new TodoManager();
        for (var i = 0; i < 10; i++)
        {
            m.Add($"item {i}");
        }

        var output = m.Status();

        Assert.StartsWith("Todos (0/10 done):", output, StringComparison.Ordinal);
        Assert.DoesNotContain("showing", output, StringComparison.Ordinal);
        Assert.DoesNotContain("not shown", output, StringComparison.Ordinal);
        Assert.DoesNotContain("list all", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_LimitBoundary_CountsOmittedStatuses()
    {
        var m = new TodoManager();
        var todos = Enumerable.Range(0, 11).Select(i => m.Add($"item {i}")).ToList();
        m.Done(todos[0]);
        m.Done(todos[1]);
        m.AddDep(todos[3], todos[2]);

        var output = m.Status();

        Assert.Contains("Todos (2/11 done; showing 10):", output, StringComparison.Ordinal);
        Assert.Contains("… +1 not shown (1 done)", output, StringComparison.Ordinal);
        Assert.Equal(10, Rows(output).Count);
    }

    [Fact]
    public void EffectiveStatus_UpdatesAfterDependencyCompletes()
    {
        var m = new TodoManager();
        var dependency = m.Add("dependency");
        var dependent = m.Add("dependent", deps: [dependency]);

        Assert.Contains(dependent, m.ListTodos("blocked"));
        Assert.Contains("●", m.Status(), StringComparison.Ordinal);

        m.Done(dependency);

        Assert.Contains(dependent, m.ListTodos("open"));
        var line = m.Status().Split('\n').First(l => l.Contains("dependent", StringComparison.Ordinal));
        Assert.Contains("○", line, StringComparison.Ordinal);
        Assert.DoesNotContain("[needs:", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearDone_RemovesOnlyCompletedTodos()
    {
        var m = new TodoManager();
        var completed = m.Add("completed");
        var active = m.Add("active", deps: [completed]);
        m.Done(completed);

        Assert.Equal(1, m.ClearDone());
        Assert.Equal([active], m.ListTodos());
        Assert.Empty(active.Deps);
        Assert.Equal(0, m.ClearDone());
    }

    [Fact]
    public void Remove_PrunesDependencyReferences()
    {
        var m = new TodoManager();
        var dependency = m.Add("dependency");
        var dependent = m.Add("dependent", deps: [dependency]);

        Assert.Contains(dependent, m.ListTodos("blocked"));
        Assert.True(m.Remove(dependency));
        Assert.Empty(dependent.Deps);
        Assert.Contains(dependent, m.ListTodos("open"));
    }

    [Fact]
    public void Status_RespectsCharacterBoundByDroppingRows()
    {
        var m = new TodoManager();
        for (var i = 0; i < 10; i++)
        {
            m.Add($"{i} " + new string('x', 200));
        }

        var output = m.Status(maxChars: 300);

        Assert.True(output.Length <= 300);
        Assert.Contains("not shown", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_ZeroItemLimit_KeepsSummaryAndHints()
    {
        var m = new TodoManager();
        m.Done(m.Add("completed"));

        var output = m.Status(maxItems: 0);

        Assert.StartsWith("Todos (1/1 done; showing 0):\n  … +1 not shown (1 done)", output, StringComparison.Ordinal);
        Assert.Contains("list all: self.todo.list_todos()", output, StringComparison.Ordinal);
        Assert.Contains("prune done: self.todo.clear_done()", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_EqualTodoValues_AreCountedIndependently()
    {
        var m = new TodoManager();
        var first = m.Add("same");
        var second = m.Add("same");
        second.CreatedAt = first.CreatedAt;

        var output = m.Status(maxItems: 1);

        Assert.Contains($"[{first.Id}]", output, StringComparison.Ordinal);
        Assert.DoesNotContain($"[{second.Id}]", output, StringComparison.Ordinal);
        Assert.Contains("… +1 not shown (1 open)", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 2000, "maxItems")]
    [InlineData(10, 199, "maxChars")]
    public void Status_InvalidLimits_Throw(int maxItems, int maxChars, string name)
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => new TodoManager().Status(maxItems, maxChars));

        Assert.Equal(name, e.ParamName);
    }

    [Fact]
    public void Status_LongTitle_IsCutWithoutSplittingSurrogates()
    {
        var m = new TodoManager();
        m.Add(new string('a', 118) + "🔍🔍");

        var output = m.Status();

        Assert.Contains(new string('a', 118) + "…", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_BlankTitle_IsUntitled()
    {
        var m = new TodoManager();
        m.Add("  ");

        Assert.Contains("(untitled)", m.Status(), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_NeedsList_IsBounded()
    {
        var m = new TodoManager();
        var deps = Enumerable.Range(0, 5).Select(i => m.Add($"d{i}")).ToList();
        m.Add("root", deps: [.. deps]);

        Assert.Contains(", +2]", m.Status(), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_ActiveShowsDescriptionDependenciesAndOtherSummary()
    {
        var m = new TodoManager();
        var unrelatedOpen = m.Add("unrelated open");
        var unrelatedDone = m.Add("unrelated done");
        m.Complete(unrelatedDone);
        var leaf = m.Add("leaf dependency");
        var middle = m.Add("middle dependency", deps: [leaf]);
        var root = m.Add("active root", deps: [middle], description: "Detailed instructions for the current work.", vars: new Dictionary<string, object?> { ["owner"] = "controller" });
        m.Comment(root, "Implementation started");

        Assert.Same(root, m.Activate(root));
        Assert.Same(root, m.Active());
        var output = m.Status();

        Assert.StartsWith($"Active [{root.Id}] active root\nStatus: blocked · 2 dependencies · 1 var · 1 comment", output, StringComparison.Ordinal);
        Assert.Contains("Description: Detailed instructions for the current work.", output, StringComparison.Ordinal);
        Assert.Contains("Recent activity: Implementation started", output, StringComparison.Ordinal);
        Assert.Contains("record material progress with self.todo.comment(id, ...)", output, StringComparison.Ordinal);
        Assert.Equal([leaf.Id, middle.Id], Rows(output).Select(RowId));
        Assert.Contains("Other Todos: 1 open · 1 done", output, StringComparison.Ordinal);
        Assert.DoesNotContain("unrelated open", output, StringComparison.Ordinal);
        Assert.DoesNotContain("unrelated done", output, StringComparison.Ordinal);
        Assert.Contains("clear active: self.todo.deactivate()", output, StringComparison.Ordinal);
        Assert.Equal([unrelatedOpen, unrelatedDone, leaf, middle, root], m.ListTodos());
    }

    [Fact]
    public void Activate_ReplacesPreviousAndDeactivateRestoresRegularStatus()
    {
        var m = new TodoManager();
        var first = m.Add("first");
        var second = m.Add("second");

        m.Activate(first);
        Assert.Same(second, m.Activate(second));
        Assert.Same(second, m.Active());
        Assert.Contains("Other Todos: 1 open", m.Status(), StringComparison.Ordinal);
        Assert.Contains($"self.todo.comment(\"{second.Id}\", \"what changed or was learned\")", m.Status(), StringComparison.Ordinal);

        Assert.Same(second, m.Deactivate());
        Assert.Null(m.Active());
        Assert.Contains("first", m.Status(), StringComparison.Ordinal);
        Assert.Contains("second", m.Status(), StringComparison.Ordinal);
        Assert.Null(m.Deactivate());
    }

    [Fact]
    public void Activate_UnmanagedOrCompleted_Throws()
    {
        var m = new TodoManager();
        var other = new TodoManager().Add("other");
        var completed = m.Add("completed");
        m.Complete(completed);

        Assert.Contains("not managed", Assert.Throws<Nooa.Errors.ValidationException>(() => m.Activate(other)).Message, StringComparison.Ordinal);
        Assert.Contains("already done", Assert.Throws<Nooa.Errors.ValidationException>(() => m.Activate(completed)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ClearsActiveOnlyForActiveRoot()
    {
        var m = new TodoManager();
        var dependency = m.Add("dependency");
        var root = m.Add("root", deps: [dependency]);
        m.Activate(root);

        Assert.Same(dependency, m.Complete(dependency));
        Assert.Equal(TodoStatus.Done, dependency.Status);
        Assert.Same(root, m.Active());

        Assert.Same(root, m.Complete(root));
        Assert.Null(m.Active());
    }

    [Fact]
    public void OtherCompletionPaths_ClearActiveRoot()
    {
        var m = new TodoManager();
        var viaUpdate = m.Add("updated");
        m.Activate(viaUpdate);
        m.Update(viaUpdate, status: TodoStatus.Done);
        Assert.Null(m.Active());

        var direct = m.Add("direct");
        m.Activate(direct);
        direct.Status = TodoStatus.Done;
        Assert.Null(m.Active());
    }

    [Fact]
    public void RemoveClearAndClearDone_ClearActiveRoot()
    {
        var m = new TodoManager();
        var removed = m.Add("removed");
        m.Activate(removed);
        Assert.True(m.Remove(removed));
        Assert.Null(m.Active());

        var cleared = m.Add("cleared");
        m.Activate(cleared);
        m.Clear();
        Assert.Null(m.Active());

        var done = m.Add("clear done");
        m.Activate(done);
        done.Status = TodoStatus.Done;
        Assert.Equal(1, m.ClearDone());
        Assert.Null(m.Active());
    }

    [Fact]
    public void Status_ActiveBoundsLongDescriptionAndMissingDependencies()
    {
        var m = new TodoManager();
        m.Activate(m.Add("root", deps: ["missing-" + new string('x', 500)], description: new string('n', 500)));

        var output = m.Status(maxItems: 0, maxChars: 200);

        Assert.True(output.Length <= 200);
        Assert.EndsWith("… status truncated", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_ActiveDeepGraphAndCycles_DoNotRecurse()
    {
        var m = new TodoManager();
        var dependency = m.Add("dependency 0");
        for (var i = 1; i <= 1_100; i++)
        {
            dependency = m.Add($"dependency {i}", deps: [dependency]);
        }

        var root = m.Add("root", deps: [dependency]);
        m.AddDep(m.ListTodos()[0], root);
        m.Activate(root);

        var output = m.Status(maxItems: 3);

        Assert.StartsWith($"Active [{root.Id}] root", output, StringComparison.Ordinal);
        Assert.Contains("+1098 dependencies not shown", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_ActiveDependencyOrder_IsDependencyFirstForSharedSiblings()
    {
        var m = new TodoManager();
        var shared = m.Add("shared");
        var dependent = m.Add("dependent", deps: [shared]);
        m.Activate(m.Add("root", deps: [dependent, shared]));

        Assert.Equal([shared.Id, dependent.Id], Rows(m.Status()).Select(RowId));
    }

    [Fact]
    public void Status_BoundsFramingWhenNoRowsFit()
    {
        var m = new TodoManager();
        for (var i = 0; i < 1_000; i++)
        {
            m.Add($"todo {i}");
        }

        var output = m.Status(maxItems: 0, maxChars: 200);

        Assert.True(output.Length <= 200);
        Assert.Contains("1000 open", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_WithoutActive_ShowsOnlyTwoNewestDoneRows()
    {
        var m = new TodoManager();
        foreach (var todo in Enumerable.Range(0, 8).Select(i => m.Add($"finished {i}")).ToList())
        {
            m.Done(todo);
        }

        var lines = m.Status().Split('\n');

        Assert.Equal("Todos (8/8 done; showing 2):", lines[0]);
        Assert.Contains("finished 7", lines[1], StringComparison.Ordinal);
        Assert.Contains("finished 6", lines[2], StringComparison.Ordinal);
        Assert.Equal("  … +6 not shown (6 done)", lines[3]);
        Assert.DoesNotContain(lines, l => Enumerable.Range(0, 6).Any(i => l.Contains($"finished {i}", StringComparison.Ordinal)));
    }

    [Fact]
    public void Status_KeepsOpenRowsAndCapsDoneRows()
    {
        var m = new TodoManager();
        for (var i = 0; i < 5; i++)
        {
            m.Done(m.Add($"finished {i}"));
        }

        var blocker = m.Add("blocker");
        m.Add("blocked", deps: [blocker]);
        m.Add("open");

        var output = m.Status();
        var rows = Rows(output);

        Assert.StartsWith("Todos (5/8 done; showing 5):", output, StringComparison.Ordinal);
        Assert.Equal(["blocker", "open", "blocked", "finished 4", "finished 3"], rows.Select(r => r.Split("] ", 2)[1].Split(" ·")[0].Split(" [needs")[0]));
        Assert.Contains("… +3 not shown (3 done)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Doc_ShowsTaskMethodsAndHidesAdministrativeOnes()
    {
        var output = AgentDocs.Doc(new TodoManager());

        foreach (var shown in new[] { "Add", "Complete", "Activate", "Status", "SetVar", "GetVar", "Comments" })
        {
            Assert.Contains($" {shown}(", output, StringComparison.Ordinal);
        }

        foreach (var hidden in new[] { "Remove", "Clear", "Reopen", "ClearDone", "Deactivate", "Active", "AddDep", "RemoveDep", "DelVar", "Done", "ToJson", "Restore", "CopyTodo", "WithTodo", "MergeTodo" })
        {
            Assert.DoesNotContain($" {hidden}(", output, StringComparison.Ordinal);
        }
    }
}
