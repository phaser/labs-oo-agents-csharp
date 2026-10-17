// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Nooa.Tools;

/// <summary>
/// Options for a persistent bash session. A shell runs commands as the host user, so the caller must create one on purpose.
/// There is no default instance.
/// </summary>
public sealed record ShellOptions
{
    /// <summary>The start directory. The shell starts there, and file operations resolve relative paths against the shell directory.</summary>
    public required string Cwd { get; init; }

    /// <summary>A shell snippet that runs once each time the session starts, before any command. Use it to set up the environment.</summary>
    public string? InitCommand { get; init; }

    /// <summary>
    /// When true, closing the session ends bash only and background jobs keep running. Their output pipes close,
    /// so a job that writes to the terminal after the close gets SIGPIPE. Redirect the job output to keep it alive.
    /// </summary>
    public bool KeepBackgroundOnClose { get; init; }

    /// <summary>
    /// Extra environment variables for the shell. The shell gets only PATH, HOME, LANG, TERM, USER, SHELL and TMPDIR
    /// from the host, so no API key reaches it by default. A variable here is passed on purpose.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ExtraEnvironment { get; init; }

    /// <summary>A logger for session events (start, restart, recovery). The logger never receives command output.</summary>
    public ILogger? Logger { get; init; }
}
