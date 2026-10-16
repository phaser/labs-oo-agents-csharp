// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Storage;

/// <summary>
/// Size limits for a session database. A file can come from an untrusted source, so reads stop at these limits.
/// This is the one place where the defaults live.
/// </summary>
public sealed record StorageLimits
{
    /// <summary>The default limits: 64 MiB per row, 1,000,000 events, 1 GiB of event data.</summary>
    public static StorageLimits Default { get; } = new();

    /// <summary>The largest stored event or snapshot, in bytes.</summary>
    public long MaxBlobBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>The largest number of events in one database.</summary>
    public long MaxEventCount { get; init; } = 1_000_000;

    /// <summary>The largest total size of all event data, in bytes.</summary>
    public long MaxTotalEventBytes { get; init; } = 1024L * 1024 * 1024;

    /// <summary>The most event data that one read page holds in memory, in bytes.</summary>
    internal const long PageBytes = 16L * 1024 * 1024;

    /// <summary>The most rows in one read page.</summary>
    internal const int PageRows = 100;
}
