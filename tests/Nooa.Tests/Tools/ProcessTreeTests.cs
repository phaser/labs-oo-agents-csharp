// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Nooa.Tools;

namespace Nooa.Tests.Tools;

public class ProcessTreeTests
{
    private static Process StartTree()
    {
        var info = new ProcessStartInfo("/bin/bash") { UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("sleep 60 & (sleep 60 & wait) & wait");
        return Process.Start(info)!;
    }

    private static async Task<Dictionary<int, string?>> WaitForTreeAsync(int root, IReadOnlySet<int> exclude, int minimum)
    {
        for (var i = 0; i < 100; i++)
        {
            var snapshot = await ProcessTree.SnapshotAsync(root, exclude);
            if (snapshot.Count >= minimum)
            {
                return snapshot;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return await ProcessTree.SnapshotAsync(root, exclude);
    }

    [Fact]
    public async Task SnapshotAsync_FindsDescendantsAtAnyDepth()
    {
        using var root = StartTree();
        try
        {
            var snapshot = await WaitForTreeAsync(root.Id, new HashSet<int>(), 3);

            Assert.True(snapshot.Count >= 3, $"found {snapshot.Count}");
            Assert.DoesNotContain(root.Id, snapshot.Keys);
            if (OperatingSystem.IsLinux())
            {
                Assert.All(snapshot.Values, identity => Assert.NotNull(identity));
            }
        }
        finally
        {
            root.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task SnapshotAsync_ExcludedProcess_LeavesOutItsSubtree()
    {
        using var root = StartTree();
        try
        {
            var all = await WaitForTreeAsync(root.Id, new HashSet<int>(), 3);
            var subshell = all.Keys.First(pid => all.Keys.Count(child => child != pid) > 0 && IsParentOfAnother(pid, all.Keys));

            var rest = await ProcessTree.SnapshotAsync(root.Id, new HashSet<int> { subshell });

            Assert.DoesNotContain(subshell, rest.Keys);
            Assert.True(rest.Count <= all.Count - 2, $"all {all.Count}, rest {rest.Count}");
        }
        finally
        {
            root.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task SnapshotAsync_NoSuchProcess_ReturnsEmpty()
    {
        Assert.Empty(await ProcessTree.SnapshotAsync(int.MaxValue - 1, new HashSet<int>()));
    }

    private static bool IsParentOfAnother(int pid, IEnumerable<int> candidates)
    {
        // A subshell that holds a background sleep has a child. Check with the kernel view, not with a name.
        var snapshot = ProcessTree.SnapshotAsync(pid, new HashSet<int>()).GetAwaiter().GetResult();
        return snapshot.Keys.Any(child => candidates.Contains(child));
    }
}
