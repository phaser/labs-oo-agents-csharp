// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Globalization;
using System.Text;

namespace Nooa.Tools;

/// <summary>
/// An editable file anchor from <see cref="ShellTools.ReadAsync"/> or a search. Pass it to <see cref="ShellTools.ReplaceAsync(Match, string, CancellationToken)"/>.
/// Print it to see numbered lines. Use <see cref="Slice"/> to narrow the region (1-indexed, inclusive line numbers).
/// <see cref="ResolvedPath"/> keeps the anchor on its file when the shell directory changes.
/// </summary>
public sealed class Match
{
    /// <summary>Creates an anchor.</summary>
    /// <param name="path">The path as the caller gave it.</param>
    /// <param name="start">The first line (1-indexed).</param>
    /// <param name="end">The last line (1-indexed, inclusive).</param>
    /// <param name="text">The raw text of the region, with line breaks.</param>
    /// <param name="resolvedPath">The absolute path of the file. A relative path throws, because it would bind to the process directory and not the shell directory.</param>
    public Match(string path, int start, int end, string text, string resolvedPath)
    {
        if (!System.IO.Path.IsPathFullyQualified(resolvedPath))
        {
            throw new ArgumentException($"Match.resolvedPath must be absolute, got \"{resolvedPath}\"", nameof(resolvedPath));
        }

        Path = path;
        ResolvedPath = System.IO.Path.GetFullPath(resolvedPath);
        Start = start;
        End = end;
        Text = text;
    }

    /// <summary>The path as the caller gave it.</summary>
    public string Path { get; }

    /// <summary>The absolute path captured when the anchor was created.</summary>
    public string ResolvedPath { get; }

    /// <summary>The first line (1-indexed).</summary>
    public int Start { get; }

    /// <summary>The last line (1-indexed, inclusive).</summary>
    public int End { get; }

    /// <summary>The raw text of the region, without line numbers.</summary>
    public string Text { get; }

    /// <summary>The text with a line-number gutter.</summary>
    public string Numbered
    {
        get
        {
            var width = End.ToString(CultureInfo.InvariantCulture).Length;
            var builder = new StringBuilder();
            var number = Start;
            foreach (var line in TextLines.Split(Text))
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(number.ToString(CultureInfo.InvariantCulture).PadLeft(width)).Append("| ").Append(line.TrimEnd('\n'));
                number++;
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Narrows the region. A null bound keeps the current bound. A bound outside the region is clamped to the region.
    /// </summary>
    /// <param name="start">The first line to keep (1-indexed).</param>
    /// <param name="end">The last line to keep (1-indexed, inclusive).</param>
    public Match Slice(int? start = null, int? end = null)
    {
        var lines = TextLines.Split(Text);
        var from = Math.Max(start ?? Start, Start);
        var to = Math.Min(end ?? End, End);
        var skip = Math.Min(from - Start, lines.Count);
        var take = Math.Clamp(to - Start + 1 - skip, 0, lines.Count - skip);
        return new Match(Path, from, to, string.Concat(lines.Skip(skip).Take(take)), ResolvedPath);
    }

    public override string ToString() => Numbered;

    /// <summary>A short description: path and line range.</summary>
    public string Describe() => $"Match(\"{Path}\", lines {Start}-{End})";
}
