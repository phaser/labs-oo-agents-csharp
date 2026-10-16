// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class WidthLimitedWriterTests
{
    [Fact]
    public void Overflowed_TextShorterThanWidth_IsFalse()
    {
        using var writer = new WidthLimitedWriter(5);
        writer.Write("abcd");
        Assert.False(writer.Overflowed);
    }

    [Fact]
    public void Overflowed_TextAtWidth_IsTrue()
    {
        using var writer = new WidthLimitedWriter(5);
        writer.Write("abcde");
        Assert.True(writer.Overflowed);
    }
}
