// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class CallArgumentTests
{
    [Fact]
    public void Equality_ComparesNameTypeAndValue()
    {
        Assert.Equal(new CallArgument("a", typeof(int), 1), new CallArgument("a", typeof(int), 1));
        Assert.NotEqual(new CallArgument("a", typeof(int), 1), new CallArgument("a", typeof(int), 2));
    }
}
