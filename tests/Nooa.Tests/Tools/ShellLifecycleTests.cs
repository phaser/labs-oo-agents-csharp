// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_lifecycle.py

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public sealed class ShellLifecycleTests : IAsyncLifetime
{
    private readonly ShellTestDirectory _directory = new();
    private readonly List<int> _pids = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        foreach (var pid in _pids)
        {
            ProcessProbe.Kill(pid);
        }

        _directory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PreserveBackgroundServices_FreshShellJobSurvivesDiscardAndScopeClose()
    {
        int pid;
        await using (ShellLifecycle.PreserveBackgroundServices())
        {
            var shell = new ShellTools(_directory.Options());
            Assert.True(shell.Session.KeepsBackgroundOnClose);
            var result = await shell.Session.RunAsync("sleep 60 >/dev/null 2>&1 & echo $!", TimeSpan.FromSeconds(10), null, TestContext.Current.CancellationToken);
            pid = int.Parse(result.Stdout, System.Globalization.CultureInfo.InvariantCulture);
            _pids.Add(pid);
        }

        Assert.True(ProcessProbe.IsAlive(pid));
    }

    [Fact]
    public async Task PreserveBackgroundServices_ScopeIsIsolatedAdoptsAndCleansUpOnFailure()
    {
        await using var outside = new BashSession(_directory.Options());
        await using var adopted = new ShellTools(_directory.Options());
        ShellTools? scoped = null;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var scope = ShellLifecycle.PreserveBackgroundServices();
            scope.Adopt(adopted.Session);
            scoped = new ShellTools(_directory.Options() with { KeepBackgroundOnClose = false });
            await scoped.Session.RunAsync("true", null, null, TestContext.Current.CancellationToken);
            Assert.False(outside.KeepsBackgroundOnClose);
            Assert.True(adopted.Session.KeepsBackgroundOnClose);
            Assert.True(scoped.Session.KeepsBackgroundOnClose);
            throw new InvalidOperationException("task failed");
        });

        Assert.Equal("task failed", error.Message);
        Assert.False(scoped!.Session.IsStarted);
        await using var after = new BashSession(_directory.Options());
        Assert.False(after.KeepsBackgroundOnClose);
    }

    [Fact]
    public async Task PreserveBackgroundServices_NestedScopes_RestoreTheOuterScope()
    {
        await using var outer = ShellLifecycle.PreserveBackgroundServices();
        await using (ShellLifecycle.PreserveBackgroundServices())
        {
        }

        await using var inOuter = new BashSession(_directory.Options());
        Assert.True(inOuter.KeepsBackgroundOnClose);
    }

    [Fact]
    public async Task Adopt_AfterClose_Throws()
    {
        var scope = ShellLifecycle.PreserveBackgroundServices();
        await scope.DisposeAsync();
        await using var session = new BashSession(_directory.Options());

        Assert.Throws<ShellToolException>(() => scope.Adopt(session));
    }

    [Fact]
    public async Task Dispose_FlowWithoutScope_IsUnaffected()
    {
        await using (ShellLifecycle.PreserveBackgroundServices())
        {
        }

        await using var session = new BashSession(_directory.Options());
        Assert.False(session.KeepsBackgroundOnClose);
    }
}
