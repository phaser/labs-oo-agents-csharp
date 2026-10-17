// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace Nooa.Tools;

/// <summary>Sends POSIX signals. .NET has no public API for a signal other than a kill.</summary>
internal static class PosixSignals
{
    public const int Interrupt = 2;
    public const int Kill = 9;
    public const int Terminate = 15;

    /// <summary>Sends a signal to a process. Returns false when the process does not exist or the call fails.</summary>
    public static bool Send(int pid, int signal) => NativeKill(pid, signal) == 0;

    /// <summary>Sends a signal to every process of a process group.</summary>
    public static bool SendToGroup(int groupId, int signal) => NativeKill(-groupId, signal) == 0;

    // DllImport and not LibraryImport: the generator needs AllowUnsafeBlocks for the whole project, and both parameters are plain integers.
#pragma warning disable SYSLIB1054, CA5392
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int NativeKill(int pid, int signal);
#pragma warning restore SYSLIB1054, CA5392
}
