// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

namespace Nooa.Tools;

/// <summary>
/// The result of a write or replace. <see cref="NewText"/> is the text as written. Observers report from it so what they record
/// matches the file. <see cref="ToString"/> leaves it out, because the caller already knows the text it sent.
/// </summary>
public sealed class FileWrite
{
    public FileWrite(string path, string message, string diff = "", string newText = "")
    {
        Path = path;
        Message = message;
        Diff = diff;
        NewText = newText;
    }

    /// <summary>The path as the caller gave it.</summary>
    public string Path { get; }

    /// <summary>A one-line summary.</summary>
    public string Message { get; }

    /// <summary>A short diff header. Empty when there is none.</summary>
    public string Diff { get; }

    /// <summary>The text written to the file.</summary>
    public string NewText { get; }

    public override string ToString() => Diff.Length == 0 ? Message : Message + "\n" + Diff;
}
