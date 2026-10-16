// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

using System.Net;
using System.Text;
using Nooa.Errors;

namespace Nooa.Storage;

/// <summary>
/// An exclusive lock on a session file, so that two processes do not write one session. The lock is held on
/// <c>&lt;name&gt;.lock.guard</c>. The record file <c>&lt;name&gt;.lock</c> holds <c>&lt;pid&gt; &lt;hostname&gt;</c> of the
/// owner and stays readable while the lock is held: .NET takes a mandatory file-share lock on the guard, which blocks readers.
/// A blank record means the owner closed cleanly. The operating system releases the lock when the owner dies,
/// so a crash does not block the next run.
/// </summary>
internal sealed class SessionLock : IDisposable
{
    private readonly string _recordPath;
    private readonly FileStream _guard;

    private SessionLock(string recordPath, FileStream guard)
    {
        _recordPath = recordPath;
        _guard = guard;
    }

    /// <summary>Takes the lock and writes the owner record. Throws <see cref="SessionAlreadyActiveException"/> if it is held.</summary>
    public static SessionLock Acquire(string lockPath)
    {
        RefuseSharedDirectory(lockPath);
        RefuseLink(lockPath);
        RefuseLink(lockPath + ".guard");
        FileStream guard;
        try
        {
            guard = new FileStream(lockPath + ".guard", Options(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            var ownerPid = ReadOwner(lockPath).Pid;
            var sessionId = Path.GetFileNameWithoutExtension(lockPath);
            var message = ownerPid is not null
                ? $"Session '{sessionId}' is already active in another process (pid {ownerPid}). " +
                  $"If that process is gone, remove '{lockPath}.guard' to reclaim the session."
                : $"Session '{sessionId}' is already active in another process " +
                  $"(owner pid unavailable - lock file '{lockPath}' is empty or unreadable).";
            throw new SessionAlreadyActiveException(message, sessionId, ownerPid);
        }

        // Holder now: replace any stale record with ours.
        WriteRecord(lockPath, Encoding.UTF8.GetBytes($"{Environment.ProcessId} {Dns.GetHostName()}"));
        return new SessionLock(lockPath, guard);
    }

    /// <summary>
    /// Returns the pid and host that the owner wrote, or <c>(null, null)</c> if the file is missing, blank or not parseable.
    /// </summary>
    public static (int? Pid, string? Host) ReadOwner(string lockPath)
    {
        string raw;
        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[512];
            var length = stream.Read(buffer, 0, buffer.Length);
            raw = new UTF8Encoding(false, false).GetString(buffer, 0, length).Trim();
        }
        catch (IOException)
        {
            return (null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, null);
        }

        if (raw.Length == 0)
        {
            return (null, null);
        }

        var space = raw.IndexOf(' ', StringComparison.Ordinal);
        var pidText = space < 0 ? raw : raw[..space];
        var host = space < 0 ? "" : raw[(space + 1)..].Trim();
        return int.TryParse(pidText, out var pid) ? (pid, host.Length == 0 ? null : host) : (null, null);
    }

    /// <summary>
    /// Blanks the record and releases the lock. The files stay on disk: removing the guard would let a second process
    /// lock a different file.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // A blank record tells a machine that cannot see the lock "free".
            WriteRecord(_recordPath, []);
        }
        catch (IOException)
        {
            // The release below still frees the lock.
        }

        _guard.Dispose();
    }

    // The record cannot go through the guard handle: the guard is locked against other readers (see the type summary).
    // A link is refused so that the write cannot overwrite a file that the link points at. The check and the open are
    // not atomic. A lock path in a directory that only the user can write closes the gap.
    private static void WriteRecord(string path, byte[] record)
    {
        RefuseLink(path);
        using var stream = new FileStream(path, Options(FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        stream.Write(record);
    }

    // The link check and the open are not atomic, and .NET has no O_NOFOLLOW open. Another user can only win that race
    // by creating a link in the lock directory, so the directory must not be writable by group or others.
    private static void RefuseSharedDirectory(string lockPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(lockPath))!;
        var mode = File.GetUnixFileMode(directory);
        if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new UnauthorizedAccessException(
                $"The session lock directory '{directory}' is writable by group or others. Use a directory that only the current user can write.");
        }
    }

    private static void RefuseLink(string path)
    {
        if (new FileInfo(path).LinkTarget is not null)
        {
            throw new UnauthorizedAccessException($"The session lock path '{path}' is a symbolic link. It is refused.");
        }
    }

    private static FileStreamOptions Options(FileMode mode, FileAccess access, FileShare share)
    {
        var options = new FileStreamOptions { Mode = mode, Access = access, Share = share };
        if (!OperatingSystem.IsWindows())
        {
            // Only the current user may read or write a lock file that this program creates.
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }
}
