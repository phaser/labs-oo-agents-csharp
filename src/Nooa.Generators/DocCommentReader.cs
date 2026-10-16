// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.Generators.Models;

namespace Nooa.Generators;

/// <summary>
/// Reads <c>///</c> doc comments from syntax trivia. It does not need <c>GenerateDocumentationFile</c>: without it the
/// compiler keeps the comment as plain trivia, and this reader parses the text itself.
/// </summary>
internal static class DocCommentReader
{
    /// <summary>Returns the doc comment of the first declaration of <paramref name="symbol"/> that has one.</summary>
    public static DocComment? Read(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax().AncestorsAndSelf().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax>().FirstOrDefault();
            if (declaration is not null && ReadTrivia(declaration) is { } comment)
            {
                return comment;
            }
        }

        return null;
    }

    /// <summary>Parses the doc comment text of a declaration. Returns null when there is none or the XML is invalid.</summary>
    public static DocComment? ReadTrivia(SyntaxNode declaration)
    {
        var xml = new StringBuilder();
        foreach (var trivia in declaration.GetLeadingTrivia())
        {
            var kind = trivia.Kind();
            var isDoc = kind == SyntaxKind.SingleLineDocumentationCommentTrivia
                || (kind == SyntaxKind.SingleLineCommentTrivia && trivia.ToString().StartsWith("///", StringComparison.Ordinal));
            if (!isDoc)
            {
                continue;
            }

            foreach (var line in trivia.ToFullString().Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    xml.Append(trimmed.Substring(3).TrimEnd('\r')).Append('\n');
                }
            }
        }

        return xml.Length == 0 ? null : Parse(xml.ToString());
    }

    /// <summary>Parses doc comment XML (without the <c>///</c> prefixes). Returns null on invalid XML.</summary>
    public static DocComment? Parse(string xml)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader("<doc>" + xml + "</doc>"), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        var root = document.Root!;
        var summary = Text(root.Elements("summary"));
        var remarks = Text(root.Elements("remarks"));
        string? text = summary is null ? remarks : remarks is null ? summary : summary + "\n\n" + remarks;
        var parameters = new List<NamedText>();
        foreach (var element in root.Elements("param"))
        {
            var name = (string?)element.Attribute("name");
            var value = Text(new[] { element });
            if (name is not null && value is not null)
            {
                parameters.Add(new NamedText(name, value));
            }
        }

        var returns = Text(root.Elements("returns"));
        if (text is null && parameters.Count == 0 && returns is null)
        {
            return null;
        }

        return new DocComment(text, new EquatableArray<NamedText>(parameters), returns);
    }

    private static string? Text(IEnumerable<XElement> elements)
    {
        var parts = elements.Select(e => Normalize(Render(e))).Where(t => t.Length > 0).ToList();
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static string Render(XElement element)
    {
        var builder = new StringBuilder();
        RenderChildren(element, builder);
        return builder.ToString();
    }

    private static void RenderChildren(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child:
                    RenderElement(child, builder);
                    break;
            }
        }
    }

    private static void RenderElement(XElement element, StringBuilder builder)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                builder.Append(ReferenceText(element));
                break;
            case "paramref":
            case "typeparamref":
                builder.Append((string?)element.Attribute("name"));
                break;
            case "para":
                builder.Append("\n\n");
                RenderChildren(element, builder);
                builder.Append("\n\n");
                break;
            case "br":
                builder.Append('\n');
                break;
            case "list":
                builder.Append('\n');
                RenderChildren(element, builder);
                builder.Append('\n');
                break;
            case "item":
                builder.Append("\n- ");
                RenderChildren(element, builder);
                break;
            default:
                RenderChildren(element, builder);
                break;
        }
    }

    private static string ReferenceText(XElement element)
    {
        var langword = (string?)element.Attribute("langword");
        if (langword is not null)
        {
            return langword;
        }

        var cref = (string?)element.Attribute("cref");
        if (cref is null)
        {
            return element.Value.Length > 0 ? element.Value : (string?)element.Attribute("href") ?? string.Empty;
        }

        if (cref.Length > 2 && cref[1] == ':')
        {
            cref = cref.Substring(2);
        }

        var paren = cref.IndexOf('(');
        if (paren >= 0)
        {
            cref = cref.Substring(0, paren);
        }

        var dot = cref.LastIndexOf('.');
        var name = dot >= 0 ? cref.Substring(dot + 1) : cref;
        return name.Replace('{', '<').Replace('}', '>');
    }

    /// <summary>Removes the common indent, trailing spaces, extra blank lines and surrounding blank lines.</summary>
    private static string Normalize(string text)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n').Select(l => l.TrimEnd()).ToList();
        if (lines.Count > 0)
        {
            lines[0] = lines[0].TrimStart();
        }

        var indents = lines.Skip(1).Where(l => l.Length > 0).Select(l => l.Length - l.TrimStart().Length).ToList();
        var indent = indents.Count == 0 ? 0 : indents.Min();
        var result = new List<string>();
        foreach (var line in lines.Select((l, i) => i == 0 || l.Length == 0 ? l : l.Substring(indent)))
        {
            if (line.Length == 0 && (result.Count == 0 || result[result.Count - 1].Length == 0))
            {
                continue;
            }

            result.Add(line);
        }

        while (result.Count > 0 && result[result.Count - 1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join("\n", result);
    }
}
