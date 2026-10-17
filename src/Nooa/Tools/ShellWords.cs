// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Text;

namespace Nooa.Tools;

/// <summary>Splits and quotes shell words like Python <c>shlex.split</c> and <c>shlex.quote</c> (POSIX mode, no comments).</summary>
internal static class ShellWords
{
    /// <summary>Splits a command line into words. Throws <see cref="FormatException"/> for an unclosed quote or a trailing backslash.</summary>
    public static List<string> Split(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }

                continue;
            }

            inWord = true;
            switch (c)
            {
                case '\\':
                    if (++i >= text.Length)
                    {
                        throw new FormatException("No escaped character");
                    }

                    word.Append(text[i]);
                    break;
                case '\'':
                    var closeSingle = text.IndexOf('\'', i + 1);
                    if (closeSingle < 0)
                    {
                        throw new FormatException("No closing quotation");
                    }

                    word.Append(text, i + 1, closeSingle - i - 1);
                    i = closeSingle;
                    break;
                case '"':
                    i = ReadDoubleQuoted(text, i + 1, word);
                    break;
                default:
                    word.Append(c);
                    break;
            }
        }

        if (inWord)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>Quotes a value so a POSIX shell reads it as one word.</summary>
    public static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        foreach (var c in value)
        {
            if (!IsSafe(c))
            {
                return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
            }
        }

        return value;
    }

    private static int ReadDoubleQuoted(string text, int start, StringBuilder word)
    {
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                return i;
            }

            // In double quotes a backslash escapes only a quote or a backslash.
            if (c == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
            {
                c = text[++i];
            }

            word.Append(c);
        }

        throw new FormatException("No closing quotation");
    }

    private static bool IsSafe(char c)
        => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '@' or '%' or '+' or '=' or ':' or ',' or '.' or '/' or '-';
}
