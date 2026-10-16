// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/storage/test_sqlite_public_options.py

using System.Net;
using Nooa.Errors;
using Nooa.Storage;

namespace Nooa.Tests.Storage;

public class SessionLockTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void ReadOwner_MissingOrBlank_ReturnsNulls()
    {
        Assert.Equal((null, null), SessionLock.ReadOwner(_dir.File("s.lock")));
        File.WriteAllText(_dir.File("s.lock"), "");
        Assert.Equal((null, null), SessionLock.ReadOwner(_dir.File("s.lock")));
    }

    [Fact]
    public void Acquire_WritesPidAndHost()
    {
        using var held = SessionLock.Acquire(_dir.File("s.lock"));

        Assert.Equal((Environment.ProcessId, Dns.GetHostName()), SessionLock.ReadOwner(_dir.File("s.lock")));
    }

    [Fact]
    public void Acquire_Twice_ThrowsWithOwnerPid()
    {
        using var held = SessionLock.Acquire(_dir.File("s.lock"));

        var e = Assert.Throws<SessionAlreadyActiveException>(() => SessionLock.Acquire(_dir.File("s.lock")));

        Assert.Equal(Environment.ProcessId, e.OwnerPid);
        Assert.Contains("pid " + Environment.ProcessId, e.Message);
        Assert.Equal("s", e.SessionId);
    }

    [Fact]
    public void Dispose_ReleasesTheLock_AndBlanksTheRecord()
    {
        SessionLock.Acquire(_dir.File("s.lock")).Dispose();

        Assert.Equal((null, null), SessionLock.ReadOwner(_dir.File("s.lock")));
        using var again = SessionLock.Acquire(_dir.File("s.lock"));
    }

    [Theory]
    [InlineData("1234", 1234, null)]
    [InlineData("1234 host-a", 1234, "host-a")]
    [InlineData("  77   box  ", 77, "box")]
    [InlineData("abc host", null, null)]
    public void ReadOwner_ParsesTheRecord(string content, int? pid, string? host)
    {
        File.WriteAllText(_dir.File("s.lock"), content);

        Assert.Equal((pid, host), SessionLock.ReadOwner(_dir.File("s.lock")));
    }

    [Fact]
    public void Acquire_SymlinkedGuard_IsRefusedAndTargetKept()
    {
        var target = _dir.File("victim.txt");
        File.WriteAllText(target, "keep");
        File.CreateSymbolicLink(_dir.File("s.lock.guard"), target);

        Assert.Throws<UnauthorizedAccessException>(() => SessionLock.Acquire(_dir.File("s.lock")));

        Assert.Equal("keep", File.ReadAllText(target));
    }

    [Fact]
    public void Acquire_SymlinkedRecord_IsRefusedAndTargetKept()
    {
        var target = _dir.File("victim.txt");
        File.WriteAllText(target, "keep");
        File.CreateSymbolicLink(_dir.File("s.lock"), target);

        Assert.Throws<UnauthorizedAccessException>(() => SessionLock.Acquire(_dir.File("s.lock")));

        Assert.Equal("keep", File.ReadAllText(target));
    }

    [Fact]
    public void Acquire_CreatesFilesReadableOnlyByTheUser()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var held = SessionLock.Acquire(_dir.File("s.lock"));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_dir.File("s.lock")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_dir.File("s.lock.guard")));
    }

    [Fact]
    public void Acquire_DirectoryWritableByOthers_IsRefused()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var shared = Directory.CreateDirectory(_dir.File("shared")).FullName;
        File.SetUnixFileMode(shared, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);

        var e = Assert.Throws<UnauthorizedAccessException>(() => SessionLock.Acquire(Path.Combine(shared, "s.lock")));

        Assert.Contains("writable by group or others", e.Message);
    }
}
