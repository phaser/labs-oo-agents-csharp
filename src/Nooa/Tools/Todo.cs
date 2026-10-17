// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/todo.py

using System.Globalization;
using System.Text.Json.Nodes;
using Nooa.Errors;
using Nooa.Storage;

namespace Nooa.Tools;

/// <summary>A managed task. Blocking is derived from unfinished dependencies.</summary>
public sealed class Todo
{
    /// <summary>A stable task ID.</summary>
    public string Id { get; set; } = NewId();

    /// <summary>A short, action-oriented description.</summary>
    public string Title { get; set; } = "";

    /// <summary>The stored status. Blocking is derived from dependencies.</summary>
    public TodoStatus Status { get; set; } = TodoStatus.Open;

    /// <summary>The IDs of prerequisite todos.</summary>
    public List<string> Deps { get; set; } = [];

    /// <summary>Durable task-specific metadata. Values are JSON.</summary>
    [Hidden]
    public Dictionary<string, JsonNode?> Vars { get; set; } = [];

    /// <summary>The creation time.</summary>
    public string CreatedAt { get; set; } = Now();

    /// <summary>The current scope, constraints, approach, and definition of done.</summary>
    public string Description { get; set; } = "";

    /// <summary>The append-only chronological progress journal.</summary>
    public List<TodoComment> Comments { get; set; } = [];

    /// <summary>Return true if any dependency is still open.</summary>
    [Hidden]
    public bool IsBlocked(IReadOnlyDictionary<string, Todo> allTodos) =>
        Deps.Any(id => allTodos.TryGetValue(id, out var dep) && dep.Status != TodoStatus.Done);

    /// <summary>
    /// Store durable metadata. A value that has no JSON form (see <see cref="SnapshotValues"/>) is not stored.
    /// Return true when the value is stored.
    /// </summary>
    [Hidden]
    public bool SetVar(string key, object? value)
    {
        try
        {
            Vars[key] = SnapshotValues.ToNode(value);
            return true;
        }
        catch (SerializationException)
        {
            return false;
        }
    }

    /// <summary>Return an independent deep copy.</summary>
    [Hidden]
    public Todo Copy() => new()
    {
        Id = Id,
        Title = Title,
        Status = Status,
        Deps = [.. Deps],
        Vars = Vars.ToDictionary(p => p.Key, p => p.Value?.DeepClone()),
        CreatedAt = CreatedAt,
        Description = Description,
        Comments = [.. Comments.Select(c => c with { })],
    };

    internal static string NewId() => Guid.NewGuid().ToString("N")[..8];

    internal static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
