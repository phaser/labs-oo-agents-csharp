// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace Nooa.Generators;

/// <summary>A string builder that indents by braces. It always writes <c>\n</c>.</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Raw(string text) => _builder.Append(text).Append('\n');

    public void Line(string text = "")
    {
        if (text.Length > 0)
        {
            _builder.Append(' ', _indent * 4).Append(text);
        }

        _builder.Append('\n');
    }

    public void Open()
    {
        Line("{");
        _indent++;
    }

    public void Close(string suffix = "")
    {
        _indent--;
        Line("}" + suffix);
    }

    public override string ToString() => _builder.ToString();
}
