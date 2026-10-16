// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Tests;

public class CodeWriterTests
{
    [Fact]
    public void Open_IndentsNestedLines_WithLineFeeds()
    {
        var writer = new CodeWriter();

        writer.Line("class A");
        writer.Open();
        writer.Line("int x;");
        writer.Line();
        writer.Close(";");

        Assert.Equal("class A\n{\n    int x;\n\n};\n", writer.ToString());
    }
}
