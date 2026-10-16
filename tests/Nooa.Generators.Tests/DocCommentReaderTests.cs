// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Tests;

public class DocCommentReaderTests
{
    [Fact]
    public void Parse_SummaryAndRemarks_JoinsWithBlankLine()
    {
        var doc = DocCommentReader.Parse("<summary>\n Short.\n </summary>\n<remarks>\n Long.\n </remarks>\n");

        Assert.Equal("Short.\n\nLong.", doc!.Text);
    }

    [Fact]
    public void Parse_InlineElements_RenderAsPlainText()
    {
        var doc = DocCommentReader.Parse(
            "<summary>Use <paramref name=\"x\"/> and <see cref=\"System.Collections.Generic.List{T}\"/> or <see langword=\"null\"/>, <c>code</c>.</summary>");

        Assert.Equal("Use x and List<T> or null, code.", doc!.Text);
    }

    [Fact]
    public void Parse_ParaAndList_KeepStructure()
    {
        var doc = DocCommentReader.Parse(
            "<summary>One.<para>Two.</para><list type=\"bullet\"><item><description>A</description></item><item><description>B</description></item></list></summary>");

        Assert.Equal("One.\n\nTwo.\n\n- A\n- B", doc!.Text);
    }

    [Fact]
    public void Parse_Parameters_AndReturns_AreCaptured()
    {
        var doc = DocCommentReader.Parse("<param name=\"a\">First.</param><param name=\"b\">Second.</param><returns>Result.</returns>");

        Assert.Null(doc!.Text);
        Assert.Equal([("a", "First."), ("b", "Second.")], doc.Params.Select(p => (p.Name, p.Text)));
        Assert.Equal("Result.", doc.Returns);
    }

    [Fact]
    public void Parse_CodeBlock_KeepsRelativeIndent()
    {
        var doc = DocCommentReader.Parse("<summary>\n Example:\n <code>\n   if (x)\n     y();\n </code>\n </summary>");

        Assert.Equal("Example:\n\n  if (x)\n    y();", doc!.Text);
    }

    [Theory]
    [InlineData("<summary>Unclosed")]
    [InlineData("<summary>a & b</summary>")]
    [InlineData("<!DOCTYPE d [<!ENTITY e \"boom\">]><summary>&e;</summary>")]
    public void Parse_InvalidOrDtdXml_ReturnsNull(string xml)
    {
        Assert.Null(DocCommentReader.Parse(xml));
    }

    [Fact]
    public void Parse_NoText_ReturnsNull()
    {
        Assert.Null(DocCommentReader.Parse("<summary>  </summary>"));
    }
}
