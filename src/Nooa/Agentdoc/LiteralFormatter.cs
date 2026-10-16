// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_pformat.py

using System.Globalization;
using System.Numerics;
using System.Text;

namespace Nooa.Agentdoc;

/// <summary>
/// Writes scalar values as C# literals and builds the truncation markers for strings and text.
/// </summary>
internal static class LiteralFormatter
{
    /// <summary>
    /// Formats a string as a C# literal. A string with a line break and no other control character becomes a
    /// verbatim literal (<c>@"..."</c>) that keeps the line breaks. Any other string becomes a regular literal.
    /// A string longer than <paramref name="maxString"/> becomes a marker with the head and the tail.
    /// </summary>
    public static string String(string s, int? maxString)
    {
        if (maxString is not { } max || s.Length <= max)
        {
            return Literal(s);
        }

        return Marker("string", "len", s, max);
    }

    /// <summary>Formats the head and the tail of a long text: <c>Name(len=N, [:H]="...", [-T:]="...")</c>.</summary>
    public static string Marker(string name, string lengthLabel, string text, int maxChars)
    {
        var head = (maxChars + 1) / 2;
        var tail = maxChars - head;
        var result = $"{name}({lengthLabel}={text.Length}, [:{head}]={Quote(text[..head])}";
        return tail > 0 ? result + $", [-{tail}:]={Quote(text[^tail..])})" : result + ")";
    }

    /// <summary>Formats a scalar value. Returns false for a value that is not a scalar.</summary>
    public static bool TryFormatScalar(object value, out string text)
    {
        text = value switch
        {
            bool b => b ? "true" : "false",
            char c => Quote(c),
            double d => Floating(d.ToString("R", CultureInfo.InvariantCulture), double.IsNaN(d), double.IsInfinity(d), double.IsNegative(d), "double"),
            float f => Floating(f.ToString("R", CultureInfo.InvariantCulture), float.IsNaN(f), float.IsInfinity(f), float.IsNegative(f), "float"),
            sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint or decimal or BigInteger or Int128 or UInt128
                => ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            Enum e => Enumeration(e),
            Type t => "typeof(" + TypeNameFormatter.Format(t) + ")",
            Delegate d => "<" + TypeNameFormatter.Format(d.GetType()) + ">",
            _ => string.Empty,
        };
        return text.Length > 0;
    }

    private static string Floating(string text, bool isNaN, bool isInfinity, bool isNegative, string type)
    {
        if (isNaN)
        {
            return type + ".NaN";
        }

        if (isInfinity)
        {
            return type + (isNegative ? ".NegativeInfinity" : ".PositiveInfinity");
        }

        // A whole number keeps a decimal point, so the reader can tell a double from an int.
        return text.AsSpan().IndexOfAny('.', 'E') < 0 ? text + ".0" : text;
    }

    private static string Enumeration(Enum value)
    {
        var type = value.GetType();
        var text = value.ToString();
        if (char.IsDigit(text[0]) || text[0] == '-')
        {
            return $"({TypeNameFormatter.Format(type)}){text}";
        }

        return string.Join(" | ", text.Split(", ").Select(name => type.Name + "." + name));
    }

    private static string Literal(string s)
    {
        var hasLineBreak = false;
        var needsEscape = false;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\n')
            {
                hasLineBreak = true;
            }
            else if (c is not ('\r' or '\t') && NeedsEscape(s, i))
            {
                needsEscape = true;
            }
        }

        if (hasLineBreak && !needsEscape)
        {
            return "@\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return Quote(s);
    }

    /// <summary>Formats a string as a regular C# literal with escapes.</summary>
    public static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        for (var i = 0; i < s.Length; i++)
        {
            Escape(sb, s, i, '"');
        }

        return sb.Append('"').ToString();
    }

    private static string Quote(char c)
    {
        var sb = new StringBuilder(4).Append('\'');
        Escape(sb, c.ToString(), 0, '\'');
        return sb.Append('\'').ToString();
    }

    private static void Escape(StringBuilder sb, string s, int index, char quote)
    {
        var c = s[index];
        if (c == quote || c == '\\')
        {
            sb.Append('\\').Append(c);
            return;
        }

        switch (c)
        {
            case '\n': sb.Append("\\n"); return;
            case '\r': sb.Append("\\r"); return;
            case '\t': sb.Append("\\t"); return;
            case '\0': sb.Append("\\0"); return;
            default:
                if (NeedsEscape(s, index))
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c);
                }

                return;
        }
    }

    /// <summary>True for a control character, a line separator or a surrogate that has no partner.</summary>
    private static bool NeedsEscape(string s, int index)
    {
        var c = s[index];
        if (char.IsHighSurrogate(c))
        {
            return index + 1 >= s.Length || !char.IsLowSurrogate(s[index + 1]);
        }

        if (char.IsLowSurrogate(c))
        {
            return index == 0 || !char.IsHighSurrogate(s[index - 1]);
        }

        return char.IsControl(c) || c is '\u2028' or '\u2029';
    }
}
