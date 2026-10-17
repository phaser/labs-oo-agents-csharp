// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tools;

namespace Nooa.Tests.Tools;

/// <summary>Checks and ends processes in shell tests.</summary>
internal static class ProcessProbe
{
    /// <summary>True while the process exists and is not a zombie.</summary>
    public static bool IsAlive(int pid)
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var stat = File.ReadAllText($"/proc/{pid}/stat");
                return stat[(stat.LastIndexOf(')') + 2)..].StartsWith('Z') is false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        return PosixSignals.Send(pid, 0);
    }

    public static async Task<bool> WaitDeadAsync(int pid, int milliseconds = 5000)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (!IsAlive(pid))
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }

    public static void Kill(int pid) => PosixSignals.Send(pid, PosixSignals.Kill);
}
