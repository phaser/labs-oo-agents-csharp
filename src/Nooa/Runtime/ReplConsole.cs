// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/stream_wrappers.py

namespace Nooa.Runtime;

/// <summary>
/// The <c>Console</c> that a cell sees. It writes to the writers of the running cell. The engine does not call
/// <see cref="System.Console.SetOut(TextWriter)"/>, so a cell cannot change the output of the host process.
/// </summary>
/// <remarks>
/// The script has no reference to the <c>System.Console</c> assembly, so <c>System.Console</c> does not compile in a cell.
/// Reading input is refused (upstream: <c>BlockedStdinWrapper</c>).
/// </remarks>
public sealed class ReplConsole
{
    /// <summary>The message of the exception that an input read throws.</summary>
    public const string StdinForbidden =
        "Reading from stdin is forbidden in agent-generated code. "
        + "Use function parameters or return values instead of interactive input.";

    private TextWriter _out = TextWriter.Null;
    private TextWriter _error = TextWriter.Null;

    /// <summary>The standard output writer of the running cell.</summary>
    public TextWriter Out => _out;

    /// <summary>The standard error writer of the running cell.</summary>
    public TextWriter Error => _error;

    /// <summary>Writes a value to the standard output.</summary>
    public void Write(object? value) => _out.Write(value);

    /// <summary>Writes text to the standard output.</summary>
    public void Write(string? value) => _out.Write(value);

    /// <summary>Writes formatted text to the standard output.</summary>
    public void Write(string format, params object?[] args) => _out.Write(format, args);

    /// <summary>Writes a line end to the standard output.</summary>
    public void WriteLine() => _out.WriteLine();

    /// <summary>Writes a value and a line end to the standard output.</summary>
    public void WriteLine(object? value) => _out.WriteLine(value);

    /// <summary>Writes text and a line end to the standard output.</summary>
    public void WriteLine(string? value) => _out.WriteLine(value);

    /// <summary>Writes formatted text and a line end to the standard output.</summary>
    public void WriteLine(string format, params object?[] args) => _out.WriteLine(format, args);

    /// <summary>Always throws. A cell has no input.</summary>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public string? ReadLine() => throw new InvalidOperationException(StdinForbidden);

    /// <summary>Always throws. A cell has no input.</summary>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public int Read() => throw new InvalidOperationException(StdinForbidden);

    internal void Attach(TextWriter stdout, TextWriter stderr)
    {
        _out = stdout;
        _error = stderr;
    }
}
