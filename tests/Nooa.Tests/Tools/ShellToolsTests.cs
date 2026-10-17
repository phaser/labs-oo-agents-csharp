// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_tools_modern_behavior.py

using Nooa.Tools;

namespace Nooa.Tests.Tools;

public sealed class ShellToolsTests : IAsyncLifetime
{
    private static readonly CancellationToken _ct = default;

    private readonly ShellTestDirectory _directory = new();
    private readonly ShellTools _shell;

    public ShellToolsTests()
    {
        _shell = new ShellTools(_directory.Options());
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _shell.DisposeAsync();
        _directory.Dispose();
    }

    private string Read(string name) => File.ReadAllText(_directory.File(name));

    [Fact]
    public async Task RunAsync_PersistsState()
    {
        var result = await _shell.RunAsync("echo hello", cancellationToken: TestContext.Current.CancellationToken);
        Directory.CreateDirectory(_directory.File("sub"));
        await _shell.RunAsync("cd sub", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("hello", result.Stdout);
        Assert.EndsWith("sub", (await _shell.RunAsync("pwd", cancellationToken: TestContext.Current.CancellationToken)).Stdout, StringComparison.Ordinal);
        Assert.Equal(_directory.File("sub"), _shell.Cwd);
    }

    [Fact]
    public async Task RunAsync_Failure_IsReported()
    {
        var result = await _shell.RunAsync("false", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotEqual(0, result.ReturnCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_Timeout_SetsFlagAndCode()
    {
        var result = await _shell.RunAsync("sleep 30", timeout: TimeSpan.FromMilliseconds(500), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.Equal(124, result.ReturnCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_WithCwd_RunsThereAndLeavesShellDirectory(bool absolute)
    {
        Directory.CreateDirectory(_directory.File("sub/deeper"));
        var home = _shell.Cwd;
        var target = absolute ? _directory.File("sub") : "sub";

        var result = await _shell.RunAsync("pwd", cwd: target, cancellationToken: TestContext.Current.CancellationToken);
        var nested = await _shell.RunAsync("cd deeper && pwd", cwd: target, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(_directory.File("sub"), result.Stdout);
        Assert.EndsWith("deeper", nested.Stdout, StringComparison.Ordinal);
        Assert.Equal(home, _shell.Cwd);
        Assert.Equal(home, (await _shell.RunAsync("pwd", cancellationToken: TestContext.Current.CancellationToken)).Stdout);
    }

    [Fact]
    public async Task RunAsync_MissingCwd_FailsWithoutRunningTheCommand()
    {
        var result = await _shell.RunAsync("touch marker; echo ran", cwd: "missing", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("missing", result.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("ran", result.Stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(_directory.File("marker")));
        Assert.Equal(_shell.Cwd, (await _shell.RunAsync("pwd", cancellationToken: TestContext.Current.CancellationToken)).Stdout);
    }

    [Fact]
    public async Task RunAsync_WithCwd_KeepsEnvironmentChangesAndReturnsStatus()
    {
        Directory.CreateDirectory(_directory.File("sub"));

        await _shell.RunAsync("export NOOA_CWD_PROBE=kept", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken);
        var failing = await _shell.RunAsync("false", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken);
        var custom = await _shell.RunAsync("exit_code() { return 7; }; exit_code", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("kept", (await _shell.RunAsync("echo $NOOA_CWD_PROBE", cancellationToken: TestContext.Current.CancellationToken)).Stdout);
        Assert.Equal(1, failing.ReturnCode);
        Assert.Equal(7, custom.ReturnCode);
    }

    [Fact]
    public async Task RunAsync_RelativeCwd_IgnoresCdpath()
    {
        Directory.CreateDirectory(_directory.File("sub"));
        Directory.CreateDirectory(_directory.File("elsewhere/sub"));
        await _shell.RunAsync($"export CDPATH={_directory.File("elsewhere")}", cancellationToken: TestContext.Current.CancellationToken);

        var result = await _shell.RunAsync("pwd", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(_directory.File("sub"), result.Stdout);
    }

    [Fact]
    public async Task RunAsync_WithStdin_KeepsShellChanges()
    {
        Directory.CreateDirectory(_directory.File("sub"));

        var result = await _shell.RunAsync("read value; export NOOA_STDIN_PROBE=$value", stdin: "kept\n", cancellationToken: TestContext.Current.CancellationToken);
        await _shell.RunAsync("cd sub", stdin: "ignored\n", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("kept", (await _shell.RunAsync("echo $NOOA_STDIN_PROBE", cancellationToken: TestContext.Current.CancellationToken)).Stdout);
        Assert.Equal(_directory.File("sub"), _shell.Cwd);
    }

    [Fact]
    public async Task RunAsync_WithStdinAndCwd_ReturnsToTheShellDirectory()
    {
        Directory.CreateDirectory(_directory.File("sub"));
        var home = _shell.Cwd;

        var result = await _shell.RunAsync("cat; pwd", stdin: "line\n", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal($"line\n{_directory.File("sub")}", result.Stdout);
        Assert.Equal(home, _shell.Cwd);
        Assert.Equal(1, (await _shell.RunAsync("false", stdin: "x\n", cancellationToken: TestContext.Current.CancellationToken)).ReturnCode);
        Assert.Equal("still here", (await _shell.RunAsync("echo still here", cancellationToken: TestContext.Current.CancellationToken)).Stdout);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("one\ntwo\n")]
    [InlineData("'\" $HOME $(echo nope) `pwd` λ\n")]
    public async Task RunStreamAsync_StdinIsVerbatimAndStatusIsKept(string payload)
    {
        const string Command = "cat; printf 'problem\\n' >&2; (exit 7)";

        var buffered = await _shell.RunAsync(Command, payload, TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
        var events = new List<ShellStreamEvent>();
        await foreach (var item in _shell.RunStreamAsync(Command, payload, TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        var chunks = events.OfType<StreamEvent>().ToList();
        var done = Assert.IsType<StreamDone>(events[^1]);
        Assert.Equal(payload, string.Concat(chunks.Where(e => e.Stream == StreamName.Stdout).Select(e => e.Text)));
        Assert.Equal("problem\n", string.Concat(chunks.Where(e => e.Stream == StreamName.Stderr).Select(e => e.Text)));
        Assert.Equal(7, done.ReturnCode);
        Assert.False(done.TimedOut);
        Assert.Single(events.OfType<StreamDone>());

        // The buffered run strips trailing line breaks. The stream does not.
        Assert.Equal(payload.TrimEnd('\n'), buffered.Stdout);
        Assert.Equal("problem", buffered.Stderr);
        Assert.Equal(done.ReturnCode, buffered.ReturnCode);
        Assert.Equal("ready", (await _shell.RunAsync("printf ready", cancellationToken: TestContext.Current.CancellationToken)).Stdout);
    }

    [Fact]
    public async Task RunStreamAsync_WithCwd_RunsThere()
    {
        Directory.CreateDirectory(_directory.File("sub"));

        var events = new List<ShellStreamEvent>();
        await foreach (var item in _shell.RunStreamAsync("pwd", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(_directory.File("sub"), string.Concat(events.OfType<StreamEvent>().Where(e => e.Stream == StreamName.Stdout).Select(e => e.Text)).Trim());
        Assert.Equal(0, Assert.IsType<StreamDone>(events[^1]).ReturnCode);
    }

    [Fact]
    public async Task WriteFileAsync_ThenReadAsync_RoundTrips()
    {
        var write = await _shell.WriteFileAsync("f.txt", "line1\nline2\nline3\n", _ct);

        Assert.Equal("Created f.txt (3 lines)", write.Message);
        Assert.Equal("line1\nline2\nline3\n", Read("f.txt"));
        var view = await _shell.ReadAsync("f.txt", cancellationToken: _ct);
        Assert.Contains("line2", view.Numbered, StringComparison.Ordinal);
        var window = await _shell.ReadAsync("f.txt", (2, 2), _ct);
        Assert.Equal("line2\n", window.Text);
    }

    [Fact]
    public async Task WriteFileAsync_Existing_Overwrites()
    {
        await _shell.WriteFileAsync("f.txt", "old", _ct);
        await _shell.WriteFileAsync("f.txt", "new", _ct);

        Assert.Equal("new", Read("f.txt"));
    }

    [Fact]
    public async Task WriteFileAsync_MissingParent_CreatesIt()
    {
        await _shell.WriteFileAsync("a/b/c.txt", "x", _ct);

        Assert.Equal("x", Read("a/b/c.txt"));
    }

    [Fact]
    public async Task ReplaceAsync_PathUnique_Replaces()
    {
        await _shell.WriteFileAsync("f.py", "x = 1\ny = 2\nz = 3\n", _ct);

        await _shell.ReplaceAsync("f.py", "y = 2", "y = 22", _ct);

        Assert.Equal("x = 1\ny = 22\nz = 3\n", Read("f.py"));
    }

    [Fact]
    public async Task ReplaceAsync_PathAmbiguous_Throws()
    {
        await _shell.WriteFileAsync("f.py", "a = 1\na = 1\n", _ct);

        var error = await Assert.ThrowsAsync<ShellToolException>(() => _shell.ReplaceAsync("f.py", "a = 1", "a = 2", _ct));

        Assert.Contains("matched 2 times", error.Message, StringComparison.Ordinal);
        Assert.Equal("a = 1\na = 1\n", Read("f.py"));
    }

    [Fact]
    public async Task ReplaceAsync_PathMissingText_Throws()
    {
        await _shell.WriteFileAsync("f.py", "a = 1\n", _ct);

        var error = await Assert.ThrowsAsync<ShellToolException>(() => _shell.ReplaceAsync("f.py", "zzz", "b", _ct));

        Assert.Contains("not found", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplaceAsync_EmptyOldText_Throws()
    {
        await _shell.WriteFileAsync("f.py", "a\n", _ct);

        await Assert.ThrowsAsync<ShellToolException>(() => _shell.ReplaceAsync("f.py", "", "b", _ct));
        Assert.Equal("a\n", Read("f.py"));
    }

    [Theory]
    [InlineData(null, "return a + b")]
    [InlineData(null, "")]
    [InlineData(2, "return a + b")]
    [InlineData(2, "")]
    public async Task ReplaceAsync_MatchWithOldAndNew_IsRejectedWithoutModifyingTheFile(int? line, string replacement)
    {
        const string Original = "def calc(a, b):\n    return a * b\n";
        await File.WriteAllTextAsync(_directory.File("calc.py"), Original, _ct);
        var match = await _shell.ReadAsync("calc.py", line is { } n ? (n, n) : null, _ct);

        var error = await Assert.ThrowsAsync<ShellToolException>(() => _shell.ReplaceAsync(match, "return a * b", replacement, _ct));

        Assert.Equal(Original, Read("calc.py"));
        Assert.Contains("ReplaceAsync(match, newText)", error.Message, StringComparison.Ordinal);
        Assert.Contains("ReplaceAsync(path, old, new)", error.Message, StringComparison.Ordinal);
        Assert.Contains("no file was changed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplaceAsync_MatchGuard_RunsBeforeFileAccess()
    {
        var match = new Match("missing.py", 1, 1, "old", _directory.File("missing.py"));

        await Assert.ThrowsAsync<ShellToolException>(() => _shell.ReplaceAsync(match, "old", "new", _ct));

        Assert.False(File.Exists(_directory.File("missing.py")));
    }

    [Fact]
    public async Task FileOperations_AllowPathsOutsideCwd()
    {
        using var sibling = new ShellTestDirectory();
        var relative = Path.GetRelativePath(_directory.Path, sibling.File("relative.txt"));
        var absolute = sibling.File("absolute.txt");

        await _shell.WriteFileAsync(relative, "one\ntwo\n", _ct);
        Assert.Equal("one\ntwo\n", (await _shell.ReadAsync(relative, cancellationToken: _ct)).Text);

        await _shell.ReplaceAsync(relative, "one", "changed", _ct);
        await _shell.ReplaceAsync(new Match(relative, 2, 2, "two\n", sibling.File("relative.txt")), "replaced", _ct);

        // The region reaches the end in a file that ended with a newline, so the replacement keeps the final newline.
        Assert.Equal("changed\nreplaced\n", await File.ReadAllTextAsync(sibling.File("relative.txt"), _ct));
        await _shell.WriteFileAsync(absolute, "absolute", _ct);
        Assert.Equal("absolute", (await _shell.ReadAsync(absolute, cancellationToken: _ct)).Text);
    }

    [Fact]
    public async Task ReplaceAsync_MatchFromRead_StaysBoundAfterCwdChange()
    {
        await File.WriteAllTextAsync(_directory.File("original.txt"), "before\n", _ct);
        Directory.CreateDirectory(_directory.File("other"));
        await File.WriteAllTextAsync(_directory.File("other/original.txt"), "wrong file\n", _ct);

        var sliced = (await _shell.ReadAsync("original.txt", cancellationToken: _ct)).Slice(1, 1);
        await _shell.RunAsync("cd other", cancellationToken: _ct);
        await _shell.ReplaceAsync(sliced, "after", _ct);

        Assert.Equal("after\n", Read("original.txt"));
        Assert.Equal("wrong file\n", Read("other/original.txt"));
    }

    [Fact]
    public async Task ReplaceAsync_MatchAtEof_KeepsTrailingNewline()
    {
        await _shell.WriteFileAsync("f.py", "a = 1\nb = 2\n", _ct);

        await _shell.ReplaceAsync(await _shell.ReadAsync("f.py", (2, 2), _ct), "b = 20", _ct);

        Assert.Equal("a = 1\nb = 20\n", Read("f.py"));
    }

    [Fact]
    public async Task ReplaceAsync_MatchAtEofWithoutNewline_DoesNotAddOne()
    {
        await _shell.WriteFileAsync("f.py", "a = 1\nb = 2", _ct);

        await _shell.ReplaceAsync(await _shell.ReadAsync("f.py", (2, 2), _ct), "b = 20", _ct);

        Assert.Equal("a = 1\nb = 20", Read("f.py"));
    }

    [Fact]
    public async Task CloseAsync_EndsTheShellAndItRestartsLazily()
    {
        Assert.True((await _shell.RunAsync("echo started", cancellationToken: _ct)).Success);
        Assert.True(_shell.Session.IsStarted);

        await _shell.CloseAsync();

        Assert.False(_shell.Session.IsStarted);
        Assert.Equal("restarted", (await _shell.RunAsync("echo restarted", cancellationToken: _ct)).Stdout);
    }

    [Fact]
    public void Constructor_UsesTheCanonicalDirectory()
    {
        Assert.Equal(_directory.Path, _shell.Cwd);
    }
}
