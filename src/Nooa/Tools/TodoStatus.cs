// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/todo.py

namespace Nooa.Tools;

/// <summary>The stored status of a todo. Blocking is derived from unfinished dependencies and is not stored.</summary>
public enum TodoStatus
{
    /// <summary>The todo is not finished.</summary>
    Open,

    /// <summary>The todo is finished.</summary>
    Done,
}
