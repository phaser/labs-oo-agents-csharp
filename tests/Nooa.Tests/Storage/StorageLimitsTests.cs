// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class StorageLimitsTests
{
    [Fact]
    public void Default_HasTheDocumentedValues()
    {
        var limits = StorageLimits.Default;

        Assert.Equal(64L * 1024 * 1024, limits.MaxBlobBytes);
        Assert.Equal(1_000_000, limits.MaxEventCount);
        Assert.Equal(1024L * 1024 * 1024, limits.MaxTotalEventBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Manager_NonPositiveLimit_Throws(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteStorageManager(limits: new StorageLimits { MaxBlobBytes = value }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteStorageManager(limits: new StorageLimits { MaxEventCount = value }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteStorageManager(limits: new StorageLimits { MaxTotalEventBytes = value }));
    }
}
