// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_sqlite_corruption_resilience.py

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class SessionAlreadyActiveExceptionTests
{
    [Fact]
    public void Constructor_KeepsMessageSessionAndPid()
    {
        var e = new SessionAlreadyActiveException("busy", "s1", 42);

        Assert.Equal(("busy", "s1", 42), (e.Message, e.SessionId, e.OwnerPid));
    }

    [Fact]
    public void Constructor_MessageOnly_LeavesOptionalFieldsNull()
    {
        var e = new SessionAlreadyActiveException("busy");

        Assert.Null(e.SessionId);
        Assert.Null(e.OwnerPid);
    }

    [Fact]
    public void Type_DerivesFromBase() => Assert.True(typeof(NooaException).IsAssignableFrom(typeof(SessionAlreadyActiveException)));
}
