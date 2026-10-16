// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

public class NoSnapshotAttributeTests
{
    [Fact]
    public void Usage_TargetsClassesAndMethods()
    {
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Method, AgenticAttributeTests.AttributeUsageOf(typeof(NoSnapshotAttribute)).ValidOn);
    }
}
