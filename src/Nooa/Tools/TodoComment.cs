// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/todo.py

namespace Nooa.Tools;

/// <summary>
/// An append-only, snapshot-backed progress-journal entry. Use comments for meaningful findings, decisions,
/// completed steps, and verification results. Keep the todo description aligned with the current objective;
/// comments keep the chronology of how understanding changed.
/// </summary>
public sealed record TodoComment
{
    /// <summary>A short stable ID.</summary>
    public string Id { get; set; } = Todo.NewId();

    /// <summary>The text of the entry.</summary>
    public string Body { get; set; } = "";

    /// <summary>The local creation time as <c>yyyy-MM-dd HH:mm</c>.</summary>
    public string CreatedAt { get; set; } = Todo.Now();
}
