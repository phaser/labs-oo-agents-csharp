// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_bash_session.py, test_bash_session_framing.py, test_bash_session_recovery.py,
// test_bash_session_background.py, test_bash_concurrency.py, test_sigint_recovery.py

using System.Diagnostics;
using System.Globalization;
using Nooa.Tools;

namespace Nooa.Tests.Tools;

[Collection(EnvironmentCollection.Name)]
public sealed class BashSessionTests : IAsyncLifetime
{
    // An unbalanced double quote. Parsed as shell text it swallows every later line, including the protocol.
    private const string _unparseable = "grep -n \"needle\\(/tmp --glob '!build' ";

    private static readonly TimeSpan _short = TimeSpan.FromSeconds(10);

    private readonly ShellTestDirectory _directory = new();
    private readonly List<int> _pids = [];
    private readonly List<BashSession> _sessions = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions)
        {
            await session.DisposeAsync();
        }

        foreach (var pid in _pids)
        {
            ProcessProbe.Kill(pid);
        }

        _directory.Dispose();
    }

    private BashSession Create(string? init = null, bool keepBackground = false, IReadOnlyDictionary<string, string>? extra = null)
    {
        var session = new BashSession(_directory.Options(init, keepBackground, extra));
        _sessions.Add(session);
        return session;
    }

    private async Task<BashSession> StartedAsync()
    {
        var session = Create();
        await session.StartAsync(TestContext.Current.CancellationToken);
        return session;
    }

    private async Task<int> StartJobAsync(BashSession session, string command)
    {
        var result = await session.RunAsync($"{command} & echo $!", _short, null, TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ExitCode);
        var pid = int.Parse(result.Stdout.Split('\n')[^1], CultureInfo.InvariantCulture);
        _pids.Add(pid);
        return pid;
    }

    private static Task<BashCommandResult> Run(BashSession session, string command, double seconds = 10)
        => session.RunAsync(command, TimeSpan.FromSeconds(seconds), null, TestContext.Current.CancellationToken);

    [Fact]
    public async Task RunAsync_SimpleCommand_ReturnsOutput()
    {
        var session = await StartedAsync();

        var result = await Run(session, "echo hello");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsExitCode()
    {
        var session = await StartedAsync();

        Assert.NotEqual(0, (await Run(session, "false")).ExitCode);
    }

    [Fact]
    public async Task RunAsync_StderrOutput_IsSeparate()
    {
        var session = await StartedAsync();

        var result = await Run(session, "echo oops 1>&2");

        Assert.Equal("oops", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public async Task RunAsync_Cd_PersistsAndUpdatesCwd()
    {
        var session = await StartedAsync();
        var sub = Path.Combine(_directory.Path, "deep", "nested");
        Directory.CreateDirectory(sub);

        await Run(session, $"cd {sub}");

        Assert.Equal(sub, (await Run(session, "pwd")).Stdout);
        Assert.Equal(sub, session.Cwd);
    }

    [Fact]
    public async Task RunAsync_DirectoryWithLineBreak_IsTrackedExactly()
    {
        var session = await StartedAsync();
        var sub = Path.Combine(_directory.Path, "a%0Ab\nc");
        Directory.CreateDirectory(sub);

        await Run(session, "cd a*c");

        Assert.Equal(sub, session.Cwd);
    }

    [Fact]
    public async Task RunAsync_ExportAndFunction_Persist()
    {
        var session = await StartedAsync();

        await Run(session, "export NOOA_TEST_VAR=persisted; noofn() { echo from_function; }");

        Assert.Equal("persisted", (await Run(session, "echo $NOOA_TEST_VAR")).Stdout);
        Assert.Equal("from_function", (await Run(session, "noofn")).Stdout);
    }

    [Fact]
    public async Task RunAsync_ExitStatus_IsReported()
    {
        var session = await StartedAsync();

        Assert.Equal(42, (await Run(session, "(exit 42)")).ExitCode);
    }

    [Fact]
    public async Task StartAsync_CalledTwice_IsSafe()
    {
        var session = await StartedAsync();

        await session.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("still_works", (await Run(session, "echo still_works")).Stdout);
        Assert.Equal(1, session.StartCount);
    }

    [Fact]
    public async Task RunAsync_LongOutput_IsBounded()
    {
        var session = await StartedAsync();

        var result = await Run(session, "head -c 50000 /dev/zero | tr '\\0' x");

        Assert.True(result.Stdout.Length <= 31000);
    }

    [Fact]
    public async Task RunAsync_LongOutput_KeepsHeadAndTailOfBothStreams()
    {
        var session = await StartedAsync();
        const string Command = "for c in H M T; do head -c 20000 /dev/zero | tr '\\0' $c; done; "
            + "for c in h m t; do head -c 20000 /dev/zero | tr '\\0' $c >&2; done";

        var result = await Run(session, Command);

        Assert.Equal(0, result.ExitCode);
        foreach (var (text, head, middle, tail) in new[] { (result.Stdout, 'H', 'M', 'T'), (result.Stderr, 'h', 'm', 't') })
        {
            Assert.StartsWith("<truncated-output>\nOutput too large (", text, StringComparison.Ordinal);
            Assert.Contains(new string(head, 14000), text, StringComparison.Ordinal);
            Assert.Contains(new string(tail, 14000), text, StringComparison.Ordinal);
            Assert.DoesNotContain(new string(middle, 1000), text, StringComparison.Ordinal);
            Assert.True(text.Length <= 31000);
        }
    }

    [Fact]
    public async Task RunAsync_Output_IsNotUnboundedInMemory()
    {
        var session = await StartedAsync();

        var result = await Run(session, "head -c 50000000 /dev/zero | tr '\\0' x", 60);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Stdout.Length <= 31000);
    }

    [Fact]
    public async Task CloseAsync_ThenRun_StartsAgain()
    {
        var session = await StartedAsync();
        Assert.Equal("first", (await Run(session, "echo first")).Stdout);

        await session.CloseAsync();
        Assert.False(session.IsStarted);

        Assert.Equal("second", (await Run(session, "echo second")).Stdout);
        Assert.Equal(2, session.StartCount);
    }

    [Fact]
    public async Task RunAsync_PipeAndFileOperations_Work()
    {
        var session = await StartedAsync();

        Assert.Equal("c\nb\na", (await Run(session, "printf 'a\\nb\\nc\\n' | sort -r")).Stdout);
        await Run(session, "echo 'test content' > test.txt");
        Assert.Equal("test content", (await Run(session, "cat test.txt")).Stdout);
        Assert.Equal("a", (await Run(session, "printf 'b\\na\\nc\\n' | sort | head -1")).Stdout);
        Assert.Equal("heredoc_line", (await Run(session, "cat <<EOF\nheredoc_line\nEOF")).Stdout);
        Assert.Equal("nested", (await Run(session, "echo $(echo nested)")).Stdout);
    }

    [Fact]
    public async Task RunAsync_UnbalancedQuote_ReturnsPromptlyWithSyntaxError()
    {
        var session = await StartedAsync();

        var result = await Run(session, _unparseable);

        Assert.False(result.TimedOut);
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Stderr.Contains("unexpected EOF", StringComparison.Ordinal) || result.Stderr.Contains("syntax error", StringComparison.Ordinal), result.Stderr);
    }

    [Fact]
    public async Task RunAsync_UnparseableCommand_KeepsSessionWithoutRestart()
    {
        var session = await StartedAsync();
        var before = session.StartCount;

        await Run(session, _unparseable);

        Assert.Equal("alive", (await Run(session, "echo alive")).Stdout);
        Assert.Equal(before, session.StartCount);
    }

    [Theory]
    [InlineData("echo \"unterminated")]
    [InlineData("echo 'unterminated")]
    [InlineData("for i in 1 2 3; do echo $i")]
    [InlineData("cat <<EOF\nno terminator")]
    [InlineData("echo $(")]
    public async Task RunAsync_MalformedShape_Returns(string command)
    {
        var session = await StartedAsync();

        var result = await Run(session, command);

        Assert.False(result.TimedOut);
        Assert.Equal("alive", (await Run(session, "echo alive")).Stdout);
    }

    [Fact]
    public async Task RunAsync_CatWithoutArguments_GetsEof()
    {
        var session = await StartedAsync();

        var result = await Run(session, "cat");

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public async Task RunAsync_ReadBuiltin_GetsEof()
    {
        var session = await StartedAsync();

        var result = await Run(session, "read line; echo got=$line");

        Assert.False(result.TimedOut);
        Assert.Equal("got=", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_Command200KB_Runs()
    {
        var session = await StartedAsync();
        var payload = new string('x', 200_000);

        var result = await Run(session, $"echo {payload} | wc -c", 30);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal((payload.Length + 1).ToString(CultureInfo.InvariantCulture), result.Stdout.Trim());
    }

    [Fact]
    public async Task RunAsync_Stdin_IsVerbatimAndKeepsShellState()
    {
        var session = await StartedAsync();
        const string Payload = "'\" $HOME $(echo nope) `pwd` λ\none\n";

        var result = await session.RunAsync("cat", _short, Payload, TestContext.Current.CancellationToken);
        await session.RunAsync("read value; export NOOA_STDIN_PROBE=$value", _short, "kept\n", TestContext.Current.CancellationToken);

        Assert.Equal(Payload.TrimEnd('\n'), result.Stdout);
        Assert.Equal("kept", (await Run(session, "echo $NOOA_STDIN_PROBE")).Stdout);
        Assert.Equal("ready", (await Run(session, "printf ready")).Stdout);
    }

    [Fact]
    public async Task RunAsync_AfterShellKilled_Recovers()
    {
        var session = await StartedAsync();
        ProcessProbe.Kill(session.ProcessId!.Value);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal("recovered", (await Run(session, "echo recovered")).Stdout);
    }

    [Fact]
    public async Task RunAsync_AfterShellExited_Recovers()
    {
        var session = await StartedAsync();
        await session.WriteRawAsync("exit 0\n");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal("recovered", (await Run(session, "echo recovered")).Stdout);
    }

    [Fact]
    public async Task RunAsync_ShellKilledDuringCommand_ReportsNonZero()
    {
        var session = await StartedAsync();
        var pid = session.ProcessId!.Value;
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(500, TestContext.Current.CancellationToken);
                ProcessProbe.Kill(pid);
            },
            TestContext.Current.CancellationToken);

        var result = await Run(session, "sleep 10", 5);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("alive", (await Run(session, "echo alive")).Stdout);
    }

    [Fact]
    public async Task RunAsync_LargeStdoutAndStderr_DoNotDeadlock()
    {
        var session = await StartedAsync();

        var result = await Run(session, "head -c 200000 /dev/zero | tr '\\0' A; head -c 200000 /dev/zero | tr '\\0' B >&2");

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Stdout.Length >= 30000);
        Assert.Equal("still_alive", (await Run(session, "echo still_alive")).Stdout);
    }

    [Fact]
    public async Task RunAsync_Timeout_Returns124AndFlag()
    {
        var session = await StartedAsync();

        var result = await session.RunAsync("sleep 10", TimeSpan.FromMilliseconds(500), null, TestContext.Current.CancellationToken);

        Assert.Equal(124, result.ExitCode);
        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_NaturalExit124_IsNotATimeout()
    {
        var session = await StartedAsync();

        var result = await Run(session, "bash -c 'exit 124'", 5);

        Assert.Equal(124, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_Timeout_KeepsDirectoryEnvironmentAndPartialOutput()
    {
        var session = await StartedAsync();
        var sub = Directory.CreateDirectory(Path.Combine(_directory.Path, "mydir")).FullName;
        await Run(session, $"cd {sub}; export TIMEOUT_TEST=hello");
        var watch = Stopwatch.StartNew();

        var result = await session.RunAsync("echo before_timeout; sleep 60", TimeSpan.FromSeconds(1), null, TestContext.Current.CancellationToken);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(12), watch.Elapsed.ToString());
        Assert.Equal("before_timeout", result.Stdout);
        Assert.Equal(sub, session.Cwd);
        Assert.Equal("hello", (await Run(session, "echo $TIMEOUT_TEST")).Stdout);
        Assert.Equal(1, session.StartCount);
    }

    [Theory]
    [InlineData("sleep 30 | cat")]
    [InlineData("(sleep 30)")]
    [InlineData("bash -c 'sleep 30'")]
    public async Task RunAsync_Timeout_EndsTheCommandTreeAndSessionSurvives(string command)
    {
        var session = await StartedAsync();
        await Run(session, "export NESTED_VAR=deep");

        for (var i = 0; i < 2; i++)
        {
            Assert.True((await session.RunAsync(command, TimeSpan.FromSeconds(1), null, TestContext.Current.CancellationToken)).TimedOut);
        }

        var watch = Stopwatch.StartNew();
        Assert.Equal("deep", (await Run(session, "echo $NESTED_VAR")).Stdout);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RunAsync_CallerCancellation_EndsCommandAndThrows()
    {
        var session = await StartedAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.RunAsync("sleep 30", TimeSpan.FromSeconds(30), null, cancellation.Token));

        Assert.Equal("alive", (await Run(session, "echo alive")).Stdout);
    }

    [Fact]
    public async Task RunAsync_BackgroundJob_IsTracked()
    {
        var session = Create();
        var pid = await StartJobAsync(session, "sleep 60");

        Assert.Contains(pid, session.BackgroundPids);
        await Run(session, "true");
        Assert.Contains(pid, session.BackgroundPids);
    }

    [Fact]
    public async Task RunAsync_Timeout_SparesEarlierBackgroundJobs()
    {
        var session = Create();
        var pid = await StartJobAsync(session, "sleep 60");

        var result = await session.RunAsync("sleep 30", TimeSpan.FromMilliseconds(500), null, TestContext.Current.CancellationToken);

        Assert.Equal(124, result.ExitCode);
        Assert.True(ProcessProbe.IsAlive(pid));
        Assert.Equal("still-usable", (await Run(session, "echo still-usable")).Stdout);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_Timeout_KillsTermResistantGrandchildButSparesEarlierJob(bool pipeline)
    {
        var session = Create();
        var pidFile = _directory.File("child.pid");
        var earlier = await StartJobAsync(session, "sleep 60");
        var command = $"bash -c 'sh -c \"trap \\\"\\\" TERM; echo \\$\\$ > {pidFile}; exec sleep 60\" & sleep 60'" + (pipeline ? " | cat" : string.Empty);

        var running = session.RunAsync(command, TimeSpan.FromSeconds(2), null, TestContext.Current.CancellationToken);
        var result = await running;

        Assert.Equal(124, result.ExitCode);
        var nested = int.Parse((await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken)).Trim(), CultureInfo.InvariantCulture);
        _pids.Add(nested);
        Assert.True(await ProcessProbe.WaitDeadAsync(nested));
        Assert.True(ProcessProbe.IsAlive(earlier));
        Assert.Equal("recovered", (await Run(session, "echo recovered")).Stdout);
    }

    [Fact]
    public async Task CloseAsync_KillsBackgroundJobsByDefault()
    {
        var session = Create();
        var pid = await StartJobAsync(session, "nohup sleep 60 >/dev/null 2>&1");

        await session.CloseAsync();

        Assert.True(await ProcessProbe.WaitDeadAsync(pid));
    }

    [Fact]
    public async Task CloseAsync_WithKeepBackground_EndsBashOnly()
    {
        var session = Create(keepBackground: true);
        var pid = await StartJobAsync(session, "sleep 60");
        var bash = session.ProcessId!.Value;

        await session.CloseAsync();

        Assert.True(await ProcessProbe.WaitDeadAsync(bash));
        Assert.True(ProcessProbe.IsAlive(pid));
    }

    [Fact]
    public async Task RunAsync_ConcurrentCalls_AreSerialized()
    {
        var session = await StartedAsync();

        var results = await Task.WhenAll(Run(session, "echo first"), Run(session, "echo second"));

        Assert.Equal(["first", "second"], results.Select(r => r.Stdout).Order());
        Assert.All(results, r => Assert.Equal(0, r.ExitCode));
    }

    [Fact]
    public async Task DisposeAsync_ClosesAndRejectsFurtherCommands()
    {
        var session = await StartedAsync();
        var bash = session.ProcessId!.Value;

        await session.DisposeAsync();

        Assert.True(await ProcessProbe.WaitDeadAsync(bash));
        Assert.False(session.IsStarted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => Run(session, "echo no"));
    }

    [Fact]
    public async Task RunStreamAsync_UnparseableCommand_Terminates()
    {
        var session = await StartedAsync();

        var events = await Collect(session.RunStreamAsync(_unparseable, _short, null, TestContext.Current.CancellationToken));

        var done = Assert.IsType<StreamDone>(events[^1]);
        Assert.False(done.TimedOut);
        Assert.NotEqual(0, done.ReturnCode);
    }

    [Fact]
    public async Task RunStreamAsync_YieldsOutputAndPersistsCd()
    {
        var session = await StartedAsync();
        var sub = Directory.CreateDirectory(Path.Combine(_directory.Path, "streamed")).FullName;

        var events = await Collect(session.RunStreamAsync($"cd {sub} && echo streamed", _short, null, TestContext.Current.CancellationToken));

        Assert.Equal("streamed\n", string.Concat(events.OfType<StreamEvent>().Where(e => e.Stream == StreamName.Stdout).Select(e => e.Text)));
        Assert.Equal(sub, session.Cwd);
    }

    [Fact]
    public async Task RunStreamAsync_StoppedEarly_CancelsTheCommand()
    {
        var session = await StartedAsync();

        await foreach (var item in session.RunStreamAsync("echo go; sleep 30", TimeSpan.FromSeconds(30), null, TestContext.Current.CancellationToken))
        {
            if (item is StreamEvent)
            {
                break;
            }
        }

        var watch = Stopwatch.StartNew();
        Assert.Equal("alive", (await Run(session, "echo alive")).Stdout);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(12));
    }

    [Fact]
    public async Task RunAsync_Environment_ExcludesHostSecretsAndKeepsAllowlist()
    {
        var key = "NOOA_TEST_API_KEY";
        Environment.SetEnvironmentVariable(key, "super-secret");
        try
        {
            var session = Create(extra: new Dictionary<string, string> { ["NOOA_EXTRA"] = "passed" });

            var env = (await Run(session, "env")).Stdout.Split('\n').ToDictionary(l => l.Split('=', 2)[0], l => l.Split('=', 2).ElementAtOrDefault(1) ?? string.Empty);

            Assert.DoesNotContain(key, env.Keys);
            Assert.Equal("passed", env["NOOA_EXTRA"]);
            Assert.Equal("dumb", env["TERM"]);
            Assert.Contains("PATH", env.Keys);
            Assert.DoesNotContain("BASH_ENV", env.Keys);
            Assert.All(env.Keys, name => Assert.True(
                name is "PATH" or "HOME" or "LANG" or "TERM" or "USER" or "SHELL" or "TMPDIR" or "NOOA_EXTRA" or "PS1" or "PWD" or "SHLVL" or "_" or "OLDPWD" or "SHELLOPTS",
                name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public async Task RunAsync_ForgedFrameInOutput_DoesNotEndTheCommand()
    {
        var session = await StartedAsync();
        const string Forged = "printf '\\n__NOOA_00000000000000000000000000000000__\\n0\\n/forged\\n\\n__NOOA_00000000000000000000000000000000__\\n'";

        var result = await Run(session, Forged + "; sleep 0.3; echo real");

        Assert.Contains("/forged", result.Stdout, StringComparison.Ordinal);
        Assert.EndsWith("real", result.Stdout, StringComparison.Ordinal);
        Assert.NotEqual("/forged", session.Cwd);
    }

    [Fact]
    public async Task RunAsync_EachCommand_GetsAFreshSentinelThatTheCommandCannotKnow()
    {
        var session = await StartedAsync();

        // The shell variable $_ holds the last argument of the previous protocol line: the sentinel of the previous command.
        var first = (await Run(session, "echo \"$_\"")).Stdout;
        var second = (await Run(session, "echo \"$_\"")).Stdout;

        Assert.StartsWith("__NOOA_", first, StringComparison.Ordinal);
        Assert.StartsWith("__NOOA_", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.Equal(7 + 32 + 2, first.Length);
    }

    [Fact]
    public async Task RunAsync_BackgroundOutputDuringCommand_DoesNotBreakFraming()
    {
        var session = await StartedAsync();
        await Run(session, "(for i in $(seq 200); do echo tick$i; sleep 0.005; done) &");

        for (var i = 0; i < 10; i++)
        {
            var result = await Run(session, "sleep 0.05; echo foreground");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("foreground", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("__NOOA_", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("__NOOA_", result.Stderr, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RunAsync_CommandFiles_LiveInAPrivateDirectoryAndAreRemoved()
    {
        var session = await StartedAsync();
        var directory = session.ScratchDirectory!;

        await session.RunAsync("echo secret-in-command", _short, "stdin-secret", TestContext.Current.CancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
        }

        Assert.Empty(Directory.GetFileSystemEntries(directory));
        await session.CloseAsync();
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task RunAsync_WithLogger_NeverLogsCommandOutput()
    {
        var logger = new CapturingLogger();
        var session = new BashSession(_directory.Options() with { Logger = logger });
        _sessions.Add(session);

        await Run(session, "echo SECRET_OUTPUT_123; echo SECRET_ERR_456 >&2");
        await session.RunAsync("echo SECRET_TIMEOUT_789; sleep 30", TimeSpan.FromMilliseconds(300), null, TestContext.Current.CancellationToken);
        ProcessProbe.Kill(session.ProcessId!.Value);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await Run(session, "echo after");

        Assert.DoesNotContain("SECRET_", logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Information && e.Message.Contains("echo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_InitCommand_RunsBeforeCommandsAndAgainAfterReset()
    {
        var session = Create(init: "export NOOA_INIT=ready; echo init-output");
        await session.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ready", (await Run(session, "echo $NOOA_INIT")).Stdout);

        await session.ResetAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ready", (await Run(session, "echo $NOOA_INIT")).Stdout);
    }

    [Fact]
    public async Task ResetAsync_KeepsDirectoryAndLosesEnvironment()
    {
        var session = await StartedAsync();
        var sub = Directory.CreateDirectory(Path.Combine(_directory.Path, "kept")).FullName;
        await Run(session, $"cd {sub}; export GONE=1");

        await session.ResetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(sub, (await Run(session, "pwd")).Stdout);
        Assert.Equal(string.Empty, (await Run(session, "echo $GONE")).Stdout);
    }

    [Fact]
    public void Constructor_NullOptions_Throws() => Assert.Throws<ArgumentNullException>(() => new BashSession(null!));

    private static async Task<List<ShellStreamEvent>> Collect(IAsyncEnumerable<ShellStreamEvent> source)
    {
        var events = new List<ShellStreamEvent>();
        await foreach (var item in source)
        {
            events.Add(item);
        }

        return events;
    }
}
