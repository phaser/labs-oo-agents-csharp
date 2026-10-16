// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_sqlite_corruption_resilience.py

using Microsoft.Data.Sqlite;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class SqliteSchemaTests
{
    [Theory]
    [InlineData("anything", 11, true)]
    [InlineData("anything", 26, true)]
    [InlineData("database disk image is malformed", 1, true)]
    [InlineData("file is not a database", 1, true)]
    [InlineData("Could not decode to UTF-8 column", 1, true)]
    [InlineData("database is locked", 5, false)]
    [InlineData("disk I/O error", 10, false)]
    public void IsCorruption_ClassifiesTheFailure(string message, int code, bool expected) =>
        Assert.Equal(expected, SqliteSchema.IsCorruption(new SqliteException(message, code)));
}
