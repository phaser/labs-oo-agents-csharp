// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_sqlite_corruption_resilience.py

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class CorruptDatabaseExceptionTests
{
    [Fact]
    public void Constructor_KeepsMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");

        var e = new CorruptDatabaseException("boom", inner);

        Assert.Equal("boom", e.Message);
        Assert.Same(inner, e.InnerException);
    }

    [Fact]
    public void Type_DerivesFromBase() => Assert.True(typeof(NooaException).IsAssignableFrom(typeof(CorruptDatabaseException)));
}
