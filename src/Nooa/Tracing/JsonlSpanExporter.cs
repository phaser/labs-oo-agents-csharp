// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_otlp_file_exporter.py

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nooa.Tracing;

/// <summary>
/// Writes spans as OTLP JSON (<c>TracesData</c>), one object per line, to <c>{directory}/{session id}.jsonl</c>.
/// Spans with no usable session id go to a timestamp-named default file.
/// </summary>
/// <remarks>
/// <para>
/// Traces hold prompts and results, so the directory is created owner-only (mode 0700) and the files are created
/// owner-only (mode 0600) on Unix. The exporter refuses a directory or a file that is a symbolic link, so a link planted
/// in a project cannot redirect the trace. The check is not atomic with the open: the directory must not be writable by
/// an attacker. The path comes from the caller or from the <c>TRACE_DIR</c> environment variable. No config file layer sets it.
/// </para>
/// <para>A session id becomes a file name only when it matches <c>[A-Za-z0-9._-]{1,128}</c> and does not start with a dot.</para>
/// </remarks>
public sealed partial class JsonlSpanExporter : ISpanExporter
{
    /// <summary>Environment variable that names the default trace directory.</summary>
    public const string DirectoryEnvVar = "TRACE_DIR";

    private readonly string _directory;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private string? _defaultFile;

    /// <summary>Creates the exporter and its directory.</summary>
    /// <param name="directory">The trace directory, or null for <c>TRACE_DIR</c> or <c>./traces</c>.</param>
    /// <param name="logger">Receives export failures. Null discards them.</param>
    /// <exception cref="IOException">The directory is a symbolic link.</exception>
    public JsonlSpanExporter(string? directory = null, ILogger? logger = null)
    {
        _directory = Path.GetFullPath(directory ?? DefaultDirectory());
        _logger = logger ?? NullLogger.Instance;
        if (new DirectoryInfo(_directory).LinkTarget is not null)
        {
            throw new IOException($"Trace directory {_directory} is a symbolic link; refusing to write traces through it");
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
        }
        else
        {
            Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>The path of the file for spans with no session id, or null when no such span was exported.</summary>
    public string? DefaultFile => _defaultFile;

    /// <summary>The default directory: <c>TRACE_DIR</c> when it is set, else <c>./traces</c>.</summary>
    public static string DefaultDirectory() =>
        Environment.GetEnvironmentVariable(DirectoryEnvVar) is { Length: > 0 } dir ? dir : Path.Combine(".", "traces");

    /// <inheritdoc />
    public string Describe() => $"otlp-file:{Path.Combine(_directory, "*.jsonl")}";

    /// <inheritdoc />
    public bool Export(IReadOnlyList<Activity> spans, IReadOnlyDictionary<string, object?> resource)
    {
        var ok = true;
        foreach (var group in spans.GroupBy(TargetFile))
        {
            try
            {
                var line = OtlpJson.Serialize([.. group], resource);
                lock (_lock)
                {
                    Append(group.Key, line);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Error exporting spans to {Path}", group.Key);
                ok = false;
            }
        }

        return ok;
    }

    /// <inheritdoc />
    public bool Flush(TimeSpan timeout) => true;

    /// <inheritdoc />
    public void Shutdown()
    {
    }

    private string TargetFile(Activity span)
    {
        if (span.GetTagItem(OpenInferenceAttributes.SessionId) is string session && SafeName().IsMatch(session))
        {
            return Path.Combine(_directory, session + ".jsonl");
        }

        lock (_lock)
        {
            _defaultFile ??= Path.Combine(_directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
            return _defaultFile;
        }
    }

    private void Append(string path, byte[] line)
    {
        if (new FileInfo(path).LinkTarget is not null)
        {
            throw new IOException($"Trace file {path} is a symbolic link; refusing to write through it");
        }

        var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.Read };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(path, options);
        stream.Write(line);
        stream.WriteByte((byte)'\n');
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();
}
