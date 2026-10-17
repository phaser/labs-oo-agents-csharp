// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/todo.py

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Nooa.Errors;
using Nooa.Json;

namespace Nooa.Tools;

/// <summary>
/// Track multi-step work, dependencies, metadata, and progress comments.
/// Every todo argument accepts either a <see cref="Todo"/> returned by this manager or its ID. Keep work visible
/// with <see cref="Status"/>, identify the current objective with <see cref="Activate"/>, and inspect details with
/// <see cref="Get"/>. Keep titles and descriptions aligned with the current understanding of the work. Add comments
/// after material findings, decisions, completed steps, and verification, not routine narration.
/// </summary>
/// <remarks>
/// The agent context block for this tool is <c>todo_status</c> with the expression <c>self.todo.status()</c>.
/// </remarks>
[Snapshotable]
[JsonConverter(typeof(TodoManagerJsonConverter))]
public sealed class TodoManager
{
    /// <summary>The default maximum number of rows in <see cref="Status"/>.</summary>
    public const int DefaultStatusMaxItems = 10;

    /// <summary>The default maximum number of characters in <see cref="Status"/>.</summary>
    public const int DefaultStatusMaxChars = 2_000;

    private const int _minStatusChars = 200;
    private const int _statusTitleChars = 120;
    private const int _statusMaxDeps = 3;

    // Completed rows shown when no todo is active; the rest are counted.
    private const int _statusMaxDone = 2;
    private const string _truncatedMarker = "\n… status truncated";

    private static readonly string[] _statusOrder = ["open", "blocked", "done"];

    private readonly Dictionary<string, Todo> _todos = [];
    private List<string> _order = [];
    private string? _activeId;

    /// <summary>Create a manager, optionally restored from the state that <see cref="ToJson"/> returned.</summary>
    public TodoManager(JsonObject? state = null)
    {
        if (state is not null)
        {
            Restore(state);
        }
    }

    // ── SERIALIZATION ─────────────────────────────

    /// <summary>Return snapshot state for later restoration by <see cref="Restore"/>.</summary>
    [Hidden]
    public JsonObject ToJson()
    {
        Active(); // Normalize direct mutation of the manager-owned Todo.
        var todos = _order.Where(_todos.ContainsKey).Select(id => _todos[id]).ToList();
        return new JsonObject
        {
            ["todos"] = JsonSerializer.SerializeToNode(todos, NooaJson.Options),
            ["active_id"] = _activeId,
        };
    }

    /// <summary>
    /// Atomically restore a snapshot. A legacy status that is not <c>done</c> becomes open, a legacy
    /// <c>notes</c> field becomes the description, and a comment without an ID gets one. A bad snapshot
    /// throws and leaves the manager unchanged.
    /// </summary>
    [Hidden]
    public void Restore(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var todos = new Dictionary<string, Todo>();
        var order = new List<string>();
        if (data["todos"] is not null and not JsonArray)
        {
            throw new DeserializationException("Todo snapshot field 'todos' must be a list.");
        }

        foreach (var raw in (data["todos"] as JsonArray) ?? [])
        {
            var todo = RestoreTodo(raw);
            if (!todos.TryAdd(todo.Id, todo))
            {
                throw new DeserializationException($"duplicate todo id '{todo.Id}' in snapshot");
            }

            order.Add(todo.Id);
        }

        var activeId = data["active_id"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var active = activeId is not null && todos.TryGetValue(activeId, out var found) ? found : null;
        _todos.Clear();
        foreach (var pair in todos)
        {
            _todos.Add(pair.Key, pair.Value);
        }

        _order = order;
        _activeId = active is { Status: not TodoStatus.Done } ? activeId : null;
    }

    private static Todo RestoreTodo(JsonNode? raw)
    {
        if (raw is not JsonObject source)
        {
            throw new DeserializationException("Todo snapshot entries must be JSON objects.");
        }

        var migrated = (JsonObject)source.DeepClone();
        if (migrated["description"] is null && migrated["notes"] is { } notes)
        {
            migrated.Remove("notes");
            migrated["description"] = notes;
        }

        // Legacy snapshots allowed arbitrary status strings (and null).
        // Only an explicit done value is evidence of completion.
        migrated["status"] = migrated["status"] is JsonValue s && s.TryGetValue<string>(out var text) && text == "done" ? "done" : "open";
        foreach (var key in migrated.Where(p => p.Value is null).Select(p => p.Key).ToList())
        {
            migrated.Remove(key);
        }

        try
        {
            return migrated.Deserialize<Todo>(NooaJson.Options)
                ?? throw new DeserializationException("Todo snapshot entry is empty.");
        }
        catch (JsonException e)
        {
            throw new DeserializationException($"Invalid todo in snapshot: {e.Message}", e);
        }
    }

    // ── CRUD ──────────────────────────────────────

    /// <summary>Return an independent copy of a manager-owned todo for delegation.</summary>
    [Hidden]
    public Todo CopyTodo(TodoRef todo) => Require(todo.Id).Copy();

    /// <summary>Create a manager that holds an independent copy of one delegated todo.</summary>
    [Hidden]
    public static TodoManager WithTodo(Todo todo)
    {
        ArgumentNullException.ThrowIfNull(todo);
        var manager = new TodoManager();
        var copied = todo.Copy();
        manager._todos[copied.Id] = copied;
        manager._order.Add(copied.Id);
        manager._activeId = copied.Status != TodoStatus.Done ? copied.Id : null;
        return manager;
    }

    /// <summary>
    /// Atomically merge delegated changes without overwriting concurrent edits. <paramref name="updated"/> is the
    /// worker result and <paramref name="baseTodo"/> is the copy that the worker started from. A change that the
    /// manager also made in a different way throws, and nothing is committed.
    /// </summary>
    [Hidden]
    public Todo MergeTodo(Todo updated, Todo baseTodo)
    {
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(baseTodo);

        // Freeze the worker result so the committed parent cannot alias worker state.
        updated = updated.Copy();
        if (updated.Id != baseTodo.Id)
        {
            throw new ValidationException("updated and base todos must have the same id");
        }

        var current = Require(updated.Id);
        var missing = updated.Deps.Except(baseTodo.Deps).Where(id => !_todos.ContainsKey(id)).Distinct().Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationException("delegated dependencies are not in the parent workspace: " + string.Join(", ", missing));
        }

        var candidate = current.Copy();
        MergeField("title", baseTodo.Title, updated.Title, current.Title, (a, b) => a == b, v => candidate.Title = v);
        MergeField("status", baseTodo.Status, updated.Status, current.Status, (a, b) => a == b, v => candidate.Status = v);
        MergeField("deps", baseTodo.Deps, updated.Deps, current.Deps, (a, b) => a.SequenceEqual(b), v => candidate.Deps = [.. v]);
        MergeField("description", baseTodo.Description, updated.Description, current.Description, (a, b) => a == b, v => candidate.Description = v);

        foreach (var key in baseTodo.Vars.Keys.Union(updated.Vars.Keys).ToList())
        {
            var before = baseTodo.Vars.TryGetValue(key, out var b) ? Maybe.Some(b) : default;
            var after = updated.Vars.TryGetValue(key, out var a) ? Maybe.Some(a) : default;
            if (Same(before, after))
            {
                continue;
            }

            var existing = current.Vars.TryGetValue(key, out var e) ? Maybe.Some(e) : default;
            if (!Same(existing, before) && !Same(existing, after))
            {
                throw new ValidationException($"todo '{updated.Id}' has conflicting variable '{key}'");
            }

            if (after.HasValue)
            {
                candidate.Vars[key] = after.Value?.DeepClone();
            }
            else
            {
                candidate.Vars.Remove(key);
            }
        }

        var baseCount = baseTodo.Comments.Count;
        if (!updated.Comments.Take(baseCount).SequenceEqual(baseTodo.Comments))
        {
            throw new ValidationException($"todo '{updated.Id}' modified existing comments");
        }

        if (!current.Comments.Take(baseCount).SequenceEqual(baseTodo.Comments))
        {
            throw new ValidationException($"todo '{updated.Id}' has conflicting comment changes");
        }

        var knownIds = candidate.Comments.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var comment in updated.Comments.Skip(baseCount))
        {
            if (knownIds.Add(comment.Id))
            {
                // Parent comments must remain independent of the worker result.
                candidate.Comments.Add(comment with { });
            }
        }

        // Commit only after every conflict check has succeeded, preserving the
        // authoritative Todo object's identity for existing callers.
        current.Title = candidate.Title;
        current.Status = candidate.Status;
        current.Deps = candidate.Deps;
        current.Description = candidate.Description;
        current.Vars = candidate.Vars;
        current.Comments = candidate.Comments;
        if (current.Status == TodoStatus.Done && current.Id == _activeId)
        {
            _activeId = null;
        }

        return current;

        void MergeField<T>(string name, T before, T after, T existing, Func<T, T, bool> equal, Action<T> apply)
        {
            if (equal(after, before))
            {
                return;
            }

            if (!equal(existing, before) && !equal(existing, after))
            {
                throw new ValidationException($"todo '{updated.Id}' has conflicting '{name}' changes");
            }

            apply(after);
        }

        static bool Same(Maybe x, Maybe y) =>
            x.HasValue == y.HasValue && (!x.HasValue || JsonNode.DeepEquals(x.Value, y.Value));
    }

    /// <summary>
    /// Create and return an open todo. <paramref name="deps"/> accepts todos or IDs. <paramref name="description"/>
    /// is the current scope, constraints, approach, and definition of done; revise it as understanding changes.
    /// Entries of <paramref name="vars"/> become durable metadata. A value that has no JSON form is not stored.
    /// </summary>
    public Todo Add(
        string title,
        IEnumerable<TodoRef>? deps = null,
        string description = "",
        IReadOnlyDictionary<string, object?>? vars = null)
    {
        var todo = new Todo
        {
            Title = title,
            Deps = [.. (deps ?? []).Select(d => d.Id)],
            Description = description,
        };
        foreach (var pair in vars ?? new Dictionary<string, object?>())
        {
            todo.SetVar(pair.Key, pair.Value);
        }

        _todos[todo.Id] = todo;
        _order.Add(todo.Id);
        return todo;
    }

    /// <summary>Return the matching managed todo, or <see langword="null"/> if it is missing.</summary>
    public Todo? Get(TodoRef todo) => _todos.GetValueOrDefault(todo.Id);

    /// <summary>Mark a todo done and return it, or <see langword="null"/> if it is missing.</summary>
    /// <remarks>
    /// Completing the active todo also clears the active selection. Completing one of its dependencies leaves
    /// the active task selected.
    /// </remarks>
    [Hidden]
    public Todo? Done(TodoRef todo)
    {
        var t = Get(todo);
        if (t is not null)
        {
            t.Status = TodoStatus.Done;
            if (t.Id == _activeId)
            {
                _activeId = null;
            }
        }

        return t;
    }

    /// <summary>Mark a todo done and return it, or <see langword="null"/> if it is missing.</summary>
    public Todo? Complete(TodoRef todo) => Done(todo);

    /// <summary>
    /// Mark a todo open and return it, or <see langword="null"/> if it is missing. It remains effectively
    /// blocked while a dependency is unfinished.
    /// </summary>
    [Hidden]
    public Todo? Reopen(TodoRef todo)
    {
        var t = Get(todo);
        t?.Status = TodoStatus.Open;
        return t;
    }

    /// <summary>Remove a todo and its references from dependent todos. Return false if it is missing.</summary>
    [Hidden]
    public bool Remove(TodoRef todo)
    {
        if (!_todos.Remove(todo.Id))
        {
            return false;
        }

        _order = [.. _order.Where(id => id != todo.Id)];
        if (todo.Id == _activeId)
        {
            _activeId = null;
        }

        foreach (var other in _todos.Values)
        {
            other.Deps = [.. other.Deps.Where(id => id != todo.Id)];
        }

        return true;
    }

    /// <summary>Remove every todo from the manager and clear the active task.</summary>
    [Hidden]
    public void Clear()
    {
        _todos.Clear();
        _order.Clear();
        _activeId = null;
    }

    /// <summary>Remove completed todos and return how many were removed. Their IDs leave the remaining dependency lists.</summary>
    [Hidden]
    public int ClearDone()
    {
        var doneIds = _todos.Where(p => p.Value.Status == TodoStatus.Done).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var id in doneIds)
        {
            _todos.Remove(id);
        }

        _order = [.. _order.Where(id => !doneIds.Contains(id))];
        if (_activeId is not null && doneIds.Contains(_activeId))
        {
            _activeId = null;
        }

        foreach (var other in _todos.Values)
        {
            other.Deps = [.. other.Deps.Where(id => !doneIds.Contains(id))];
        }

        return doneIds.Count;
    }

    /// <summary>
    /// Update the title, status, or description, and return the todo. Blocking is derived from unfinished
    /// dependencies; use <see cref="AddDep"/> to add a dependency. A <see langword="null"/> argument leaves the
    /// field unchanged. Use <see cref="Comment"/> to append material progress and evidence. Return
    /// <see langword="null"/> if the todo is missing.
    /// </summary>
    public Todo? Update(TodoRef todo, string? title = null, TodoStatus? status = null, string? description = null)
    {
        var t = Get(todo);
        if (t is null)
        {
            return null;
        }

        t.Title = title ?? t.Title;
        t.Status = status ?? t.Status;
        t.Description = description ?? t.Description;
        if (t.Status == TodoStatus.Done && t.Id == _activeId)
        {
            _activeId = null;
        }

        return t;
    }

    // ── ACTIVE TODO ────────────────────────────────

    /// <summary>
    /// Make one open todo the active task shown prominently by <see cref="Status"/>. A new call replaces the
    /// previous active task. The full workspace stays available through <see cref="ListTodos"/>. Throw when the
    /// todo is missing or already complete.
    /// </summary>
    public Todo Activate(TodoRef todo)
    {
        var t = Require(todo.Id);
        if (t.Status == TodoStatus.Done)
        {
            throw new ValidationException($"todo '{todo.Id}' is already done");
        }

        _activeId = todo.Id;
        return t;
    }

    /// <summary>Clear the active task and return it, if one was active.</summary>
    [Hidden]
    public Todo? Deactivate()
    {
        var todo = Active();
        _activeId = null;
        return todo;
    }

    /// <summary>Return the active todo, or <see langword="null"/> when no open todo is active.</summary>
    [Hidden]
    public Todo? Active()
    {
        if (_activeId is null)
        {
            return null;
        }

        if (!_todos.TryGetValue(_activeId, out var todo) || todo.Status == TodoStatus.Done)
        {
            _activeId = null;
            return null;
        }

        return todo;
    }

    // ── DEPENDENCIES ──────────────────────────────

    /// <summary>Add a dependency and return the todo, or <see langword="null"/> if it is missing.</summary>
    [Hidden]
    public Todo? AddDep(TodoRef todo, TodoRef dep)
    {
        var t = Get(todo);
        if (t is not null && !t.Deps.Contains(dep.Id))
        {
            t.Deps.Add(dep.Id);
        }

        return t;
    }

    /// <summary>Remove a dependency and return the todo, or <see langword="null"/> if it is missing.</summary>
    [Hidden]
    public Todo? RemoveDep(TodoRef todo, TodoRef dep)
    {
        Get(todo)?.Deps.Remove(dep.Id);
        return Get(todo);
    }

    // ── VARIABLES ─────────────────────────────────

    /// <summary>
    /// Store durable metadata and return the todo, or <see langword="null"/> if it is missing. A value that
    /// has no JSON form is not stored.
    /// </summary>
    public Todo? SetVar(TodoRef todo, string key, object? value)
    {
        var t = Get(todo);
        t?.SetVar(key, value);
        return t;
    }

    /// <summary>Delete a metadata key and return the todo, or <see langword="null"/> if it is missing.</summary>
    [Hidden]
    public Todo? DelVar(TodoRef todo, string key)
    {
        var t = Get(todo);
        t?.Vars.Remove(key);
        return t;
    }

    /// <summary>Return a metadata value, or <see langword="null"/> if the todo or key is missing.</summary>
    public JsonNode? GetVar(TodoRef todo, string key) =>
        Get(todo) is { } t && t.Vars.TryGetValue(key, out var value) ? value : null;

    // ── COMMENTS ──────────────────────────────────

    /// <summary>
    /// Append material progress and return it, or <see langword="null"/> if the todo is missing. Record
    /// meaningful findings, decisions, completed steps, and verification results, not routine narration.
    /// </summary>
    public TodoComment? Comment(TodoRef todo, string body)
    {
        var t = Get(todo);
        if (t is null)
        {
            return null;
        }

        var comment = new TodoComment { Body = body };
        t.Comments.Add(comment);
        return comment;
    }

    /// <summary>Return a chronological copy of the comments, or an empty list if none exist.</summary>
    public IReadOnlyList<TodoComment> Comments(TodoRef todo) => [.. Get(todo)?.Comments ?? []];

    // ── QUERIES ───────────────────────────────────

    /// <summary>
    /// Return todos in creation order, optionally filtered by effective status: <c>open</c>, <c>blocked</c>, or
    /// <c>done</c>. Effective blocking is derived from unfinished dependencies and updates automatically.
    /// </summary>
    public IReadOnlyList<Todo> ListTodos(string? status = null)
    {
        var todos = Ordered();
        return status is null ? todos : [.. todos.Where(t => EffectiveStatus(t) == status)];
    }

    // ── STATUS ────────────────────────────────────

    /// <summary>
    /// Return a compact, bounded progress summary. When a todo is active, show its description and transitive
    /// dependencies, followed by a compact summary of unrelated work. Otherwise list open and blocked work, then
    /// the two newest done. <see cref="ListTodos"/> always returns the full workspace.
    /// </summary>
    public string Status(int maxItems = DefaultStatusMaxItems, int maxChars = DefaultStatusMaxChars)
    {
        if (maxItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems), "max_items must be non-negative");
        }

        if (maxChars < _minStatusChars)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChars), $"max_chars must be at least {_minStatusChars}");
        }

        var todos = Ordered();
        if (todos.Count == 0)
        {
            return "(no todos)";
        }

        var active = Active();
        return active is not null ? ActiveStatus(todos, active, maxItems, maxChars) : PlainStatus(todos, maxItems, maxChars);
    }

    private string PlainStatus(List<Todo> todos, int maxItems, int maxChars)
    {
        var byStatus = _statusOrder.ToDictionary(s => s, _ => new List<Todo>());
        foreach (var todo in todos)
        {
            byStatus[EffectiveStatus(todo)].Add(todo);
        }

        // Open and blocked work keeps its rows; completed history shows only the
        // newest few and is otherwise counted in the "not shown" line.
        var recentDone = Enumerable.Reverse(byStatus["done"]).Take(_statusMaxDone);
        var selected = byStatus["open"].Concat(byStatus["blocked"]).Concat(recentDone).Take(maxItems).ToList();

        var output = Render(selected);
        while (selected.Count > 0 && output.Length > maxChars)
        {
            selected.RemoveAt(selected.Count - 1);
            output = Render(selected);
        }

        return BoundedStatus(output, maxChars);

        string Render(List<Todo> rows)
        {
            var omitted = todos.Where(t => !rows.Contains(t, ReferenceEqualityComparer.Instance)).ToList();
            var header = $"Todos ({byStatus["done"].Count}/{todos.Count} done";
            if (omitted.Count > 0)
            {
                header += $"; showing {rows.Count}";
            }

            var lines = new List<string> { header + "):" };
            lines.AddRange(rows.Select(StatusLine));
            if (omitted.Count > 0)
            {
                lines.Add($"  … +{omitted.Count} not shown ({CountSummary(omitted, ", ")})");
            }

            var hints = new List<string>();
            if (omitted.Count > 0)
            {
                hints.Add("list all: self.todo.list_todos()");
            }

            if (rows.Any(t => t.Description.Trim().Length > 0 || t.Vars.Count > 0 || t.Comments.Count > 0))
            {
                hints.Add("inspect: self.todo.get(id)");
            }

            if (byStatus["done"].Count > 0)
            {
                hints.Add("prune done: self.todo.clear_done()");
            }

            if (hints.Count > 0)
            {
                lines.Add("Hint: " + string.Join(" · ", hints));
            }

            return string.Join('\n', lines);
        }
    }

    private string ActiveStatus(List<Todo> todos, Todo active, int maxItems, int maxChars)
    {
        var (dependencies, missing) = DependencyClosure(active);

        // The active task is always represented by its detail card; maxItems
        // limits the additional dependency rows.
        var selected = dependencies.Take(maxItems).ToList();
        var output = Render(selected);
        while (selected.Count > 0 && output.EndsWith("… status truncated", StringComparison.Ordinal))
        {
            selected.RemoveAt(selected.Count - 1);
            output = Render(selected);
        }

        return output;

        string Render(List<Todo> rows)
        {
            var omittedCount = dependencies.Count - rows.Count;
            var details = new List<string> { EffectiveStatus(active) };
            var depTotal = dependencies.Count + missing.Count;
            if (depTotal > 0)
            {
                details.Add($"{depTotal} dependenc{(depTotal == 1 ? "y" : "ies")}");
            }

            if (active.Vars.Count > 0)
            {
                details.Add($"{active.Vars.Count} var{(active.Vars.Count != 1 ? "s" : "")}");
            }

            if (active.Comments.Count > 0)
            {
                details.Add($"{active.Comments.Count} comment{(active.Comments.Count != 1 ? "s" : "")}");
            }

            var lines = new List<string> { $"Active [{active.Id}] {StatusTitle(active.Title)}", $"Status: {string.Join(" · ", details)}" };
            var note = Compact(active.Description);
            lines.Add($"Description: {(note.Length > 0 ? note : "(none)")}");
            if (active.Comments.Count > 0)
            {
                lines.Add($"Recent activity: {Compact(active.Comments[^1].Body)}");
            }

            if (rows.Count > 0 || omittedCount > 0 || missing.Count > 0)
            {
                lines.Add("");
                lines.Add("Dependencies:");
                lines.AddRange(rows.Select(StatusLine));
            }

            if (omittedCount > 0)
            {
                lines.Add($"  … +{omittedCount} dependencies not shown");
            }

            if (missing.Count > 0)
            {
                lines.Add($"  ! missing dependencies: {string.Join(", ", missing)}");
            }

            var dependencyIds = dependencies.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            var other = todos.Where(t => t.Id != active.Id && !dependencyIds.Contains(t.Id)).ToList();
            if (other.Count > 0)
            {
                lines.Add("");
                lines.Add($"Other Todos: {CountSummary(other, " · ")}");
            }

            var hints = new List<string> { "clear active: self.todo.deactivate()" };
            if (note.Length == 0)
            {
                hints.Add($"refine task: self.todo.update(\"{active.Id}\", description=\"scope and next step\")");
            }

            hints.Add(active.Comments.Count == 0
                ? $"record progress: self.todo.comment(\"{active.Id}\", \"what changed or was learned\")"
                : "record material progress with self.todo.comment(id, ...)");
            hints.Add("show all: self.todo.list_todos()");
            lines.Add("Hint: " + string.Join(" · ", hints));
            return BoundedStatus(string.Join('\n', lines), maxChars);
        }
    }

    // ── HELPERS ───────────────────────────────────

    private List<Todo> Ordered() => [.. _order.Where(_todos.ContainsKey).Select(id => _todos[id])];

    private Todo Require(string id) =>
        _todos.GetValueOrDefault(id) ?? throw new ValidationException($"todo '{id}' is not managed by this TodoManager");

    private string EffectiveStatus(Todo todo) =>
        todo.Status == TodoStatus.Done ? "done" : todo.IsBlocked(_todos) ? "blocked" : "open";

    private string CountSummary(List<Todo> todos, string separator) =>
        string.Join(separator, _statusOrder.Select(s => (Status: s, Count: todos.Count(t => EffectiveStatus(t) == s))).Where(p => p.Count > 0).Select(p => $"{p.Count} {p.Status}"));

    private static string Compact(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Collapse a title to one bounded display line.</summary>
    private static string StatusTitle(string title)
    {
        var compact = Compact(title);
        if (compact.Length == 0)
        {
            compact = "(untitled)";
        }

        return compact.Length <= _statusTitleChars ? compact : Cut(compact, _statusTitleChars - 1).TrimEnd() + "…";
    }

    /// <summary>Apply a final hard bound even when fixed status framing is large.</summary>
    private static string BoundedStatus(string output, int maxChars) =>
        output.Length <= maxChars ? output : Cut(output, maxChars - _truncatedMarker.Length).TrimEnd() + _truncatedMarker;

    private static string Cut(string text, int length)
    {
        if (length > 0 && char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length];
    }

    private string StatusLine(Todo todo)
    {
        var effective = EffectiveStatus(todo);
        var icon = effective switch { "open" => "○", "done" => "✓", "blocked" => "●", _ => "?" };
        var unresolved = todo.Deps.Where(id => _todos.TryGetValue(id, out var dep) && EffectiveStatus(dep) != "done").ToList();
        var depText = "";
        if (unresolved.Count > 0)
        {
            var shown = unresolved.Take(_statusMaxDeps).ToList();
            var suffix = unresolved.Count > shown.Count ? $", +{unresolved.Count - shown.Count}" : "";
            depText = $" [needs: {string.Join(", ", shown)}{suffix}]";
        }

        var details = new List<string>();
        if (todo.Description.Trim().Length > 0)
        {
            details.Add("description");
        }

        if (todo.Vars.Count > 0)
        {
            details.Add($"{todo.Vars.Count} var{(todo.Vars.Count != 1 ? "s" : "")}");
        }

        if (todo.Comments.Count > 0)
        {
            details.Add($"{todo.Comments.Count} comment{(todo.Comments.Count != 1 ? "s" : "")}");
        }

        var detailText = details.Count > 0 ? $" · {string.Join(" · ", details)}" : "";
        return new StringBuilder("  ").Append(icon).Append(" [").Append(todo.Id).Append("] ").Append(StatusTitle(todo.Title)).Append(depText).Append(detailText).ToString();
    }

    /// <summary>Return the transitive dependencies before <paramref name="root"/>, without recursion.</summary>
    private (List<Todo> Ordered, List<string> Missing) DependencyClosure(Todo root)
    {
        var ordered = new List<Todo>();
        var state = new Dictionary<string, int> { [root.Id] = 1 }; // 1 = visiting, 2 = emitted
        var missing = new List<string>();
        var stack = new Stack<(Todo Todo, int Index)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (todo, index) = stack.Pop();
            if (index >= todo.Deps.Count)
            {
                state[todo.Id] = 2;
                if (todo.Id != root.Id)
                {
                    ordered.Add(todo);
                }

                continue;
            }

            stack.Push((todo, index + 1));
            var depId = todo.Deps[index];
            if (!_todos.TryGetValue(depId, out var dependency))
            {
                if (!missing.Contains(depId))
                {
                    missing.Add(depId);
                }

                continue;
            }

            if (state.GetValueOrDefault(dependency.Id) == 0)
            {
                state[dependency.Id] = 1;
                stack.Push((dependency, 0));
            }
        }

        return (ordered, missing);
    }

    private readonly record struct Maybe(bool HasValue, JsonNode? Value)
    {
        public static Maybe Some(JsonNode? value) => new(true, value);
    }
}
