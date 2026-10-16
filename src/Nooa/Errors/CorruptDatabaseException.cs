// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

namespace Nooa.Errors;

/// <summary>
/// A write failed because the SQLite file is corrupt. Callers can use this type to show recovery guidance.
/// </summary>
public class CorruptDatabaseException : NooaException
{
    public CorruptDatabaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
