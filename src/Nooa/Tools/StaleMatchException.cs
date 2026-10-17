// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Text;

namespace Nooa.Tools;

/// <summary>
/// A <see cref="Match"/> edit was rejected because the file no longer holds the captured text at the captured lines.
/// The message has a bounded diff. The exception keeps no copy of the stored or current text.
/// </summary>
public sealed class StaleMatchException : ShellToolException
{
    /// <summary>The stable error code.</summary>
    public const string ErrorCode = "STALE_MATCH";

    private const int _maxInputChars = 16 * 1024;
    private const int _maxInputLines = 400;
    private const int _maxPreviewLines = 20;
    private const int _maxPreviewBytes = 2048;

    public StaleMatchException(Match target, string? current, string reason)
        : this(target, reason, StaleDiff(target.Text, current))
    {
    }

    private StaleMatchException(Match target, string reason, (string Diff, bool Complete, string Detail) diff)
        : base(BuildMessage(target, reason, diff))
    {
        Path = target.Path;
        ResolvedPath = target.ResolvedPath;
        Start = target.Start;
        End = target.End;
        Reason = reason;
        Diff = diff.Diff;
        DiffComplete = diff.Complete;
    }

    /// <summary>The path as the caller gave it.</summary>
    public string Path { get; }

    /// <summary>The absolute path of the file.</summary>
    public string ResolvedPath { get; }

    /// <summary>The first captured line.</summary>
    public int Start { get; }

    /// <summary>The last captured line.</summary>
    public int End { get; }

    /// <summary>Why the edit was rejected.</summary>
    public string Reason { get; }

    /// <summary>A preview of the diff, at most 20 lines and 2 KiB. Empty when no diff was computed.</summary>
    public string Diff { get; }

    /// <summary>True when <see cref="Diff"/> is the whole diff.</summary>
    public bool DiffComplete { get; }

    private static string BuildMessage(Match target, string reason, (string Diff, bool Complete, string Detail) diff)
    {
        var message = new StringBuilder()
            .Append($"{ErrorCode}: \"{target.Path}\" (resolved path: \"{target.ResolvedPath}\"), captured lines {target.Start}-{target.End}: {reason}. ")
            .Append("No file was changed. Re-read or re-search the file and recompute the Match before retrying; anchors are never relocated.")
            .Append('\n').Append(diff.Detail);
        if (diff.Diff.Length > 0)
        {
            message.Append('\n').Append(diff.Diff);
            if (!diff.Complete)
            {
                // Byte truncation can leave the preview in the middle of a line. The notice stays on its own line.
                if (!diff.Diff.EndsWith('\n'))
                {
                    message.Append('\n');
                }

                message.Append("Diff preview truncated (capped at 20 lines and 2 KiB); not a complete diff.");
            }
        }

        return message.ToString();
    }

    // Bounds the computation and the output: at most 16 KiB and 400 input lines, at most 20 lines and 2 KiB of preview.
    private static (string Diff, bool Complete, string Detail) StaleDiff(string expected, string? current)
    {
        if (current is null)
        {
            return ("", false, "Diff omitted (incomplete): current region is unavailable.");
        }

        if (expected.Length + current.Length > _maxInputChars
            || Encoding.UTF8.GetByteCount(expected) + Encoding.UTF8.GetByteCount(current) > _maxInputChars)
        {
            return ("", false, "Diff omitted (incomplete): input regions exceed 16 KiB.");
        }

        var expectedLines = TextLines.Split(expected);
        var currentLines = TextLines.Split(current);
        if (expectedLines.Count + currentLines.Count > _maxInputLines)
        {
            return ("", false, "Diff omitted (incomplete): input regions exceed 400 lines.");
        }

        var full = UnifiedDiff.Build(expectedLines, currentLines, "stored Match.text (when read)", "current file (same saved line range)");
        var preview = string.Concat(TextLines.Split(full).Take(_maxPreviewLines));
        preview = CutUtf8(preview, _maxPreviewBytes);
        var complete = preview == full;
        var detail = complete
            ? "stored Match.text (when read) versus current file (same saved line range) diff (complete):"
            : "Diff preview:";
        return (preview, complete, detail);
    }

    // Cuts at a byte limit and drops a character that the cut splits.
    private static string CutUtf8(string text, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length <= maxBytes)
        {
            return text;
        }

        var length = maxBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
        {
            length--;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}
