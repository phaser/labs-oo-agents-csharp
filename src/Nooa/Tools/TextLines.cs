// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Text;

namespace Nooa.Tools;

/// <summary>Reads, writes and splits text files the way the shell tools expect.</summary>
internal static class TextLines
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>
    /// Reads a UTF-8 file with universal newlines: a CRLF or a lone CR becomes LF, like Python text mode.
    /// A byte order mark stays in the text as a character. Invalid UTF-8 throws.
    /// </summary>
    public static async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return _strictUtf8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    /// <summary>Writes a file as UTF-8 without a byte order mark.</summary>
    public static Task WriteAsync(string path, string content, CancellationToken cancellationToken)
        => File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken);

    /// <summary>Splits text at each LF and keeps the line breaks. No other character ends a line, so line numbers equal grep and ripgrep numbers.</summary>
    public static List<string> Split(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline + 1;
            lines.Add(text[start..end]);
            start = end;
        }

        return lines;
    }
}
