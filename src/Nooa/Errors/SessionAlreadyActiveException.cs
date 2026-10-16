// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

namespace Nooa.Errors;

/// <summary>
/// A session database is already open in another process.
/// </summary>
public class SessionAlreadyActiveException : NooaException
{
    public SessionAlreadyActiveException(string message, string? sessionId = null, int? ownerPid = null)
        : base(message)
    {
        SessionId = sessionId;
        OwnerPid = ownerPid;
    }

    /// <summary>The session identifier (the database file name without extension), if known.</summary>
    public string? SessionId { get; }

    /// <summary>The process id read from the lock file, or null if it is not available.</summary>
    public int? OwnerPid { get; }
}
