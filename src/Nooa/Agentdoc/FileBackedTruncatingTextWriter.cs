// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_truncating_stream.py

using System.Text;

namespace Nooa.Agentdoc;

/// <summary>
/// A <see cref="TruncatingTextWriter"/> that also writes all text to a temporary file. The truncation notice
/// names the file, so the full output stays readable.
/// </summary>
/// <remarks>
/// If the file cannot be created or written, the writer works in memory only and <see cref="FilePath"/> is null.
/// The file is created for the current user only. <see cref="TextWriter.Dispose()"/> closes the file and keeps it on disk.
/// <see cref="Cleanup"/> closes and deletes it.
/// </remarks>
public sealed class FileBackedTruncatingTextWriter : TruncatingTextWriter
{
    private static readonly char[] _separators = ['/', '\\', '\0', ':'];

    private StreamWriter? _file;

    /// <summary>Creates a writer and its temporary file.</summary>
    /// <param name="limit">The maximum characters to keep in memory.</param>
    /// <param name="tailChars">The characters kept for the tail. Null means half of <paramref name="limit"/>.</param>
    /// <param name="directory">The folder for the file. Null means the system temporary folder.</param>
    /// <param name="prefix">The start of the file name. It must not contain a path separator.</param>
    /// <param name="suffix">The end of the file name. It must not contain a path separator.</param>
    public FileBackedTruncatingTextWriter(
        int limit = DefaultLimit,
        int? tailChars = null,
        string? directory = null,
        string prefix = "nemo_output_",
        string suffix = ".txt")
        : base(limit, tailChars)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(suffix);
        if (prefix.IndexOfAny(_separators) >= 0 || suffix.IndexOfAny(_separators) >= 0)
        {
            throw new ArgumentException("prefix and suffix must not contain a path separator.");
        }

        try
        {
            var path = Path.Combine(directory ?? Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N") + suffix);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            _file = new StreamWriter(path, new UTF8Encoding(false), options) { AutoFlush = true };
            FilePath = path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _file = null;
        }
    }

    /// <summary>The path of the file, or null when no file was created or writing to it failed.</summary>
    public string? FilePath { get; private set; }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (_file is not null)
        {
            try
            {
                _file.Write(buffer);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // The notice then says that the dropped text is not recoverable.
                _file = null;
                FilePath = null;
            }
        }

        base.Write(buffer);
    }

    /// <summary>Closes the file and deletes it. The in-memory text stays readable.</summary>
    public void Cleanup()
    {
        var path = FilePath;
        Dispose();
        if (path is not null)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // The file may be in use. Nothing else needs it.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <inheritdoc />
    protected override string RecoveryNotice(long total, long dropped)
        => FilePath is null
            ? base.RecoveryNotice(total, dropped)
            : $"The full untruncated output ({Count(total)} chars) is in: {FilePath}\nRead that file to see the {Count(dropped)} chars not shown here.\n";

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _file?.Dispose();
        }

        base.Dispose(disposing);
    }
}
