// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Text.Encodings.Web;
using System.Text.Json;

namespace Nooa.Tools;

/// <summary>
/// The result of <see cref="ShellTools.RunAsync"/>. <see cref="Stdout"/> is the command output. <see cref="ToString"/> shows stderr and a
/// non-zero return code as named fields, so a failure with empty stdout cannot look like blank output.
/// <see cref="Matches"/> is a list when the command was a pure search and the anchors are trustworthy, and null otherwise.
/// </summary>
public sealed class ShellResult
{
    private static readonly JsonSerializerOptions _quote = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public ShellResult(string stdout, string stderr = "", int returnCode = 0, IReadOnlyList<Match>? matches = null, bool timedOut = false)
    {
        Stdout = stdout;
        Stderr = stderr;
        ReturnCode = returnCode;
        Matches = matches;
        TimedOut = timedOut;
    }

    /// <summary>The standard output.</summary>
    public string Stdout { get; }

    /// <summary>The standard error.</summary>
    public string Stderr { get; }

    /// <summary>The exit code. 124 means timeout.</summary>
    public int ReturnCode { get; }

    /// <summary>True when the command ran past its timeout.</summary>
    public bool TimedOut { get; }

    /// <summary>True when <see cref="ReturnCode"/> is 0.</summary>
    public bool Success => ReturnCode == 0;

    /// <summary>Editable anchors of a pure search, or null.</summary>
    public IReadOnlyList<Match>? Matches { get; }

    public override string ToString()
    {
        var parts = new List<string> { $"stdout={Quote(Stdout)}" };
        if (Stderr.Length > 0)
        {
            parts.Add($"stderr={Quote(Stderr)}");
        }

        if (ReturnCode != 0)
        {
            parts.Add($"return_code={ReturnCode}");
        }

        return $"BashOutput({string.Join(", ", parts)})";
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text, _quote);
}
