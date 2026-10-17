// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/_bash_session.py

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Nooa.Tools;

/// <summary>Finds the descendants of a process. A timeout uses it to end one command and spare earlier background jobs.</summary>
internal static class ProcessTree
{
    private const int _maxChildren = 4096;
    private static readonly TimeSpan _psTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Returns the descendants of <paramref name="rootPid"/>, with the Linux start time that identifies each process.
    /// A process in <paramref name="exclude"/> is left out together with its subtree. An empty result means no descendant or a failed scan.
    /// </summary>
    public static async Task<Dictionary<int, string?>> SnapshotAsync(int rootPid, IReadOnlySet<int> exclude)
    {
        if (OperatingSystem.IsMacOS() && TryWalkLibproc(rootPid, exclude) is { } walked)
        {
            return walked;
        }

        var children = OperatingSystem.IsLinux() ? ReadProc() : await ReadPsAsync().ConfigureAwait(false);
        var result = new Dictionary<int, string?>();
        if (children is null)
        {
            return result;
        }

        var pending = new Stack<(int Pid, string? Identity)>();
        if (children.TryGetValue(rootPid, out var first))
        {
            foreach (var child in first)
            {
                pending.Push(child);
            }
        }

        while (pending.Count > 0)
        {
            var (pid, identity) = pending.Pop();
            if (exclude.Contains(pid) || result.ContainsKey(pid))
            {
                continue;
            }

            result[pid] = identity;
            if (children.TryGetValue(pid, out var next))
            {
                foreach (var child in next)
                {
                    pending.Push(child);
                }
            }
        }

        return result;
    }

    // macOS: ask the kernel for the children of each process. This starts no process, so it stays fast when the machine is busy.
    // Returns null when libproc is not usable. The caller then falls back to ps.
    private static Dictionary<int, string?>? TryWalkLibproc(int rootPid, IReadOnlySet<int> exclude)
    {
        var result = new Dictionary<int, string?>();
        var pending = new Stack<int>();
        pending.Push(rootPid);
        var buffer = new int[_maxChildren];
        try
        {
            while (pending.Count > 0)
            {
                var count = ListChildPids(pending.Pop(), buffer, buffer.Length * sizeof(int));
                if (count < 0)
                {
                    return null;
                }

                foreach (var pid in buffer.Take(Math.Min(count, buffer.Length)))
                {
                    if (pid > 0 && !exclude.Contains(pid) && result.TryAdd(pid, null))
                    {
                        pending.Push(pid);
                    }
                }
            }

            return result;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    // DllImport and not LibraryImport: see PosixSignals. The buffer is an array of plain integers.
#pragma warning disable SYSLIB1054, CA5392
    [DllImport("libproc", EntryPoint = "proc_listchildpids")]
    private static extern int ListChildPids(int parentPid, int[] buffer, int bufferSize);
#pragma warning restore SYSLIB1054, CA5392

    /// <summary>Returns the Linux start time of a process, so a signal cannot reach a process that reused the PID. Null when unknown.</summary>
    public static string? StartTime(int pid)
    {
        try
        {
            var fields = ParseStat(File.ReadAllText($"/proc/{pid}/stat"));
            return fields?.StartTime;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Dictionary<int, List<(int Pid, string? Identity)>>? ReadProc()
    {
        var children = new Dictionary<int, List<(int, string?)>>();
        try
        {
            foreach (var entry in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(entry), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                try
                {
                    var fields = ParseStat(File.ReadAllText(Path.Combine(entry, "stat")));
                    if (fields is null)
                    {
                        continue;
                    }

                    if (!children.TryGetValue(fields.Value.ParentPid, out var list))
                    {
                        children[fields.Value.ParentPid] = list = [];
                    }

                    list.Add((pid, fields.Value.StartTime));
                }
                catch (IOException)
                {
                    // The process ended between the listing and the read.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return children;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<Dictionary<int, List<(int Pid, string? Identity)>>?> ReadPsAsync()
    {
        try
        {
            var info = new ProcessStartInfo("ps")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("-eo");
            info.ArgumentList.Add("pid=,ppid=");
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            using var timeout = new CancellationTokenSource(_psTimeout);
            try
            {
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                var output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await errors.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    return null;
                }

                var children = new Dictionary<int, List<(int, string?)>>();
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2
                        || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                        || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent))
                    {
                        continue;
                    }

                    if (!children.TryGetValue(parent, out var list))
                    {
                        children[parent] = list = [];
                    }

                    list.Add((pid, null));
                }

                return children;
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return null;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    // The command name sits in parentheses and can contain spaces and parentheses. Fields after the last ')' are stable.
    private static (int ParentPid, string StartTime)? ParseStat(string stat)
    {
        var close = stat.LastIndexOf(')');
        if (close < 0)
        {
            return null;
        }

        var fields = stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20 || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent))
        {
            return null;
        }

        return (parent, fields[19]);
    }
}
