// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/todo.py

namespace Nooa.Tools;

/// <summary>A reference to a todo. A <see cref="Todo"/> or its ID converts to it implicitly.</summary>
/// <param name="Id">The ID of the todo.</param>
public readonly record struct TodoRef(string Id)
{
    /// <summary>Refer to a todo by object.</summary>
    public static implicit operator TodoRef(Todo todo)
    {
        ArgumentNullException.ThrowIfNull(todo);
        return new TodoRef(todo.Id);
    }

    /// <summary>Refer to a todo by ID.</summary>
    public static implicit operator TodoRef(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return new TodoRef(id);
    }
}
