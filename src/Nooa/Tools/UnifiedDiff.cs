// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Globalization;
using System.Text;

namespace Nooa.Tools;

/// <summary>Builds a unified diff of two line lists, with three lines of context, in the format of Python <c>difflib.unified_diff</c>.</summary>
internal static class UnifiedDiff
{
    private const int _context = 3;

    /// <summary>Returns the diff text. A line without a trailing line break gets a "No newline at end of file" note. Returns an empty string for equal inputs.</summary>
    public static string Build(IReadOnlyList<string> expected, IReadOnlyList<string> current, string fromFile, string toFile)
    {
        var edits = Edits(expected, current);
        var hunks = Hunks(edits);
        if (hunks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("--- ").Append(fromFile).Append('\n').Append("+++ ").Append(toFile).Append('\n');
        foreach (var hunk in hunks)
        {
            var first = edits[hunk.From];
            var oldCount = 0;
            var newCount = 0;
            for (var i = hunk.From; i < hunk.To; i++)
            {
                oldCount += edits[i].Kind != '+' ? 1 : 0;
                newCount += edits[i].Kind != '-' ? 1 : 0;
            }

            builder.Append("@@ -").Append(Range(first.OldIndex, oldCount)).Append(" +").Append(Range(first.NewIndex, newCount)).Append(" @@\n");
            for (var i = hunk.From; i < hunk.To; i++)
            {
                var edit = edits[i];
                var line = edit.Kind == '+' ? current[edit.NewIndex] : expected[edit.OldIndex];
                builder.Append(edit.Kind == ' ' ? ' ' : edit.Kind).Append(line);
                if (!line.EndsWith('\n'))
                {
                    builder.Append("\n\\ No newline at end of file\n");
                }
            }
        }

        return builder.ToString();
    }

    private static string Range(int zeroBasedStart, int length)
    {
        var beginning = zeroBasedStart + 1;
        if (length == 1)
        {
            return beginning.ToString(CultureInfo.InvariantCulture);
        }

        if (length == 0)
        {
            beginning--;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{beginning},{length}");
    }

    // An edit script from a longest-common-subsequence table. Inputs are small: the caller caps them at 400 lines.
    private static List<Edit> Edits(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var table = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var edits = new List<Edit>();
        int x = 0, y = 0;
        while (x < a.Count || y < b.Count)
        {
            if (x < a.Count && y < b.Count && a[x] == b[y])
            {
                edits.Add(new Edit(' ', x++, y++));
            }
            else if (x < a.Count && (y == b.Count || table[x + 1, y] >= table[x, y + 1]))
            {
                edits.Add(new Edit('-', x++, y));
            }
            else
            {
                edits.Add(new Edit('+', x, y++));
            }
        }

        return edits;
    }

    // Groups changed edits with their context. Two changes closer than twice the context share one hunk.
    private static List<(int From, int To)> Hunks(List<Edit> edits)
    {
        var hunks = new List<(int From, int To)>();
        var i = 0;
        while (i < edits.Count)
        {
            if (edits[i].Kind == ' ')
            {
                i++;
                continue;
            }

            var from = Math.Max(0, i - _context);
            var to = i;
            var lastChange = i;
            while (to < edits.Count && to - lastChange <= (2 * _context) + 1)
            {
                if (edits[to].Kind != ' ')
                {
                    lastChange = to;
                }

                to++;
            }

            to = Math.Min(edits.Count, lastChange + 1 + _context);
            hunks.Add((from, to));
            i = to;
        }

        return hunks;
    }

    private readonly record struct Edit(char Kind, int OldIndex, int NewIndex);
}
