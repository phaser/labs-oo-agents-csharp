// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

namespace Nooa.Storage;

/// <summary>SQLite journal mode for a session database.</summary>
/// <remarks>
/// Choose <see cref="Delete"/> for a file that another machine may open over a shared mount: WAL keeps cross-process
/// state in a shared-memory file that two kernels do not share. A file left in WAL mode is converted on open.
/// </remarks>
public enum SqliteJournalMode
{
    /// <summary>Write-ahead log. It is the default.</summary>
    Wal,

    /// <summary>Rollback journal.</summary>
    Delete,
}
