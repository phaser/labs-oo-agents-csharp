// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/_bash_session.py

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nooa.Tools;

/// <summary>
/// A persistent bash shell. State (<c>cd</c>, <c>export</c>, functions) carries over between commands. POSIX only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Security.</b> The shell runs commands as the host user, with no sandbox. It gets an allowlisted environment (PATH, HOME, LANG,
/// TERM, USER, SHELL, TMPDIR and <see cref="ShellOptions.ExtraEnvironment"/>), so host secrets do not reach it by default.
/// </para>
/// <para>
/// <b>Protocol.</b> The command goes to a private file that bash sources with its stdin closed. Bash reads only the protocol lines from the
/// pipe. After each command bash prints a frame to stdout and a marker to stderr. Both hold a fresh random sentinel for that command, so
/// text that a command prints cannot forge a frame. The frame holds the exit status, the directory and the PIDs of background jobs.
/// The sentinel reaches bash only after the command ends. A command that deliberately reads the shell's own stdin pipe could learn it.
/// The shell is not a sandbox.
/// </para>
/// <para>
/// A command that redirects the shell's own stdout or stderr (<c>exec &gt;file</c>) hides the frame. The command then times out and the
/// session restarts, which loses the shell state.
/// </para>
/// <para>
/// Calls are serialized. Concurrent <see cref="RunAsync"/> calls queue. For parallel commands, create several sessions.
/// The shell runs in its own process group. Disposing the session ends the group, unless <see cref="ShellOptions.KeepBackgroundOnClose"/> is set.
/// </para>
/// </remarks>
public sealed class BashSession : IAsyncDisposable
{
    /// <summary>The characters kept of each of a command's stdout and stderr: the first and the last half.</summary>
    public const int MaxOutputChars = 30_000;

    /// <summary>The exit code of a command that ran past its timeout, like <c>timeout(1)</c>.</summary>
    public const int TimeoutExitCode = 124;

    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _handshakeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _initTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _terminateGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _killGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _closeGrace = TimeSpan.FromSeconds(3);
    private static readonly UTF8Encoding _utf8 = new(false);
    private static readonly string[] _inheritedVariables = ["PATH", "HOME", "LANG", "USER", "SHELL", "TMPDIR"];
    private static readonly string[] _setsidPaths = ["/usr/bin/setsid", "/bin/setsid", "/usr/local/bin/setsid"];
    private static readonly string[] _perlPaths = ["/usr/bin/perl", "/bin/perl", "/usr/local/bin/perl"];

    private readonly ShellOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string _cwd;
    private bool _keepBackgroundOnClose;
    private bool _started;
    private bool _disposed;
    private Process? _process;
    private StreamWriter? _stdin;
    private BashStreamCollector? _stdout;
    private BashStreamCollector? _stderr;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private string? _scratchDirectory;
    private IReadOnlySet<int> _backgroundPids = new HashSet<int>();

    /// <summary>Creates a session. The shell starts on the first command, or on <see cref="StartAsync"/>.</summary>
    /// <exception cref="PlatformNotSupportedException">The platform is Windows.</exception>
    public BashSession(ShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("BashSession needs a POSIX system with bash. Windows is not supported.");
        }

        _options = options;
        _logger = options.Logger ?? NullLogger.Instance;
        _cwd = NooaPaths.Canonicalize(options.Cwd);
        _keepBackgroundOnClose = options.KeepBackgroundOnClose;
        ShellLifecycle.Current?.Adopt(this);
    }

    /// <summary>The current directory of the shell.</summary>
    public string Cwd => _cwd;

    /// <summary>True while a shell process runs.</summary>
    public bool IsStarted => _started;

    /// <summary>How many times the shell started: the first start, a restart after a failure and each reset.</summary>
    internal int StartCount { get; private set; }

    /// <summary>The private directory for the command files, or null when the shell is not started.</summary>
    internal string? ScratchDirectory => _scratchDirectory;

    /// <summary>True when closing the session leaves background jobs running.</summary>
    internal bool KeepsBackgroundOnClose => _keepBackgroundOnClose;

    /// <summary>The PID of the bash process, or null when none runs.</summary>
    internal int? ProcessId => _process?.Id;

    /// <summary>The PIDs of background jobs after the last completed command.</summary>
    internal IReadOnlySet<int> BackgroundPids => _backgroundPids;

    /// <summary>Starts the shell. Does nothing when it runs.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_started)
            {
                await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Runs a command and returns its output and exit code. State persists: <c>cd</c> and <c>export</c> carry over to the next command.
    /// On timeout the command tree ends, earlier background jobs survive, and the exit code is 124.
    /// </summary>
    /// <param name="command">The shell text.</param>
    /// <param name="timeout">The time limit. Null means 30 seconds.</param>
    /// <param name="stdin">Text for the stdin of the command. Null means the command reads an empty stdin.</param>
    /// <param name="cancellationToken">Cancels the wait. The command ends like on a timeout, and the call throws <see cref="OperationCanceledException"/>.</param>
    public async Task<BashCommandResult> RunAsync(string command, TimeSpan? timeout = null, string? stdin = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunLockedAsync(command, stdin, timeout ?? _defaultTimeout, null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Runs a command and yields its output as it arrives, then a <see cref="StreamDone"/>. Chunks are not trimmed or bounded.
    /// Stop the enumeration early to cancel the command.
    /// </summary>
    public async IAsyncEnumerable<ShellStreamEvent> RunStreamAsync(
        string command,
        TimeSpan? timeout = null,
        string? stdin = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<ShellStreamEvent>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var run = Task.Run(
            async () =>
            {
                try
                {
                    await _lock.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    try
                    {
                        var result = await RunLockedAsync(
                            command,
                            stdin,
                            timeout ?? _defaultTimeout,
                            (stream, text) => channel.Writer.TryWrite(new StreamEvent(stream, text)),
                            cancellation.Token).ConfigureAwait(false);
                        channel.Writer.TryWrite(new StreamDone(result.ExitCode, result.TimedOut));
                    }
                    finally
                    {
                        _lock.Release();
                    }

                    channel.Writer.TryComplete();
                }
                catch (Exception error)
                {
                    channel.Writer.TryComplete(error);
                }
            },
            CancellationToken.None);
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            await run.ConfigureAwait(false);
        }
    }

    /// <summary>Ends the shell and starts a new one in the same directory. Shell state is lost.</summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ResetCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Ends the shell. The session can start again on the next command. The process group ends, except when
    /// <see cref="ShellOptions.KeepBackgroundOnClose"/> is set.
    /// </summary>
    public async Task CloseAsync()
    {
        await CloseCoreAsync().ConfigureAwait(false);
    }

    /// <summary>Ends the shell for good. A later command throws <see cref="ObjectDisposedException"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await CloseCoreAsync().ConfigureAwait(false);
    }

    /// <summary>Keeps background jobs alive when the session closes. A background scope sets this.</summary>
    internal void KeepBackgroundOnClose() => _keepBackgroundOnClose = true;

    /// <summary>Writes text to the shell stdin without the protocol. A test uses it to break the session.</summary>
    internal async Task WriteRawAsync(string text)
    {
        await _stdin!.WriteAsync(text).ConfigureAwait(false);
        await _stdin.FlushAsync().ConfigureAwait(false);
    }

    private static string NewSentinel() => "__NOOA_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + "__";

    private static string DecodeDirectory(string encoded)
        => encoded.Replace("%0A", "\n", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal);

    private static async Task PumpAsync(StreamReader reader, BashStreamCollector collector)
    {
        var buffer = new char[8192];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
            {
                collector.Append(buffer.AsSpan(0, read));
            }
        }
        catch (IOException)
        {
            // The pipe closed.
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            collector.MarkEof();
        }
    }

    private static (string FileName, string[] Arguments) ShellCommand()
    {
        string[] bash = ["/bin/bash", "--norc", "--noprofile"];
        var setsid = _setsidPaths.FirstOrDefault(File.Exists);
        if (setsid is not null)
        {
            return (setsid, bash);
        }

        var perl = _perlPaths.FirstOrDefault(File.Exists);
        if (perl is not null)
        {
            return (perl, ["-MPOSIX", "-e", "POSIX::setsid() >= 0 or die qq(setsid failed\\n); exec @ARGV or die qq(exec failed\\n)", .. bash]);
        }

        throw new ShellToolException(
            "BashSession needs setsid or perl to start bash in its own process group, and found neither. Install util-linux (setsid) or perl.");
    }

    private async Task<BashCommandResult> RunLockedAsync(
        string command,
        string? stdin,
        TimeSpan timeout,
        Action<StreamName, string>? sink,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);
        if (!_started)
        {
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        return await ExecuteAsync(command, stdin, timeout, sink, cancellationToken).ConfigureAwait(false);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        StartCount++;
        _scratchDirectory ??= Directory.CreateTempSubdirectory("nooa-shell-").FullName;
        var (fileName, arguments) = ShellCommand();
        var info = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = _cwd,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = _utf8,
            StandardOutputEncoding = _utf8,
            StandardErrorEncoding = _utf8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        BuildEnvironment(info.Environment);
        Process process;
        try
        {
            process = Process.Start(info) ?? throw new ShellToolException("bash did not start.");
        }
        catch (Win32Exception error)
        {
            throw new ShellToolException($"bash did not start: {error.Message}", error);
        }

        _process = process;
        _stdin = process.StandardInput;
        _stdin.AutoFlush = true;
        _stdout = new BashStreamCollector(MaxOutputChars, expectPayload: true);
        _stderr = new BashStreamCollector(MaxOutputChars, expectPayload: false);
        _stdoutPump = PumpAsync(process.StandardOutput, _stdout);
        _stderrPump = PumpAsync(process.StandardError, _stderr);
        _started = true;
        _logger.LogDebug("Started bash with process id {ProcessId} (start {StartCount}).", process.Id, StartCount);

        // The first command doubles as the handshake. The init command, when set, runs before any user command.
        var init = _options.InitCommand;
        var result = await ExecuteAsync(init ?? ":", null, init is null ? _handshakeTimeout : _initTimeout, null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == -1)
        {
            throw new ShellToolException("bash exited while it started" + (init is null ? "." : ". Check the init command."));
        }
    }

    private void BuildEnvironment(IDictionary<string, string?> environment)
    {
        environment.Clear();
        foreach (var name in _inheritedVariables)
        {
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
            {
                environment[name] = value;
            }
        }

        environment.TryAdd("PATH", "/usr/bin:/bin");
        environment["TERM"] = "dumb";
        environment["PS1"] = string.Empty;
        if (_options.ExtraEnvironment is { } extras)
        {
            foreach (var (name, value) in extras)
            {
                environment[name] = value;
            }
        }
    }

    private string BuildProtocol(string sentinel, string commandFile)
    {
        // One printf writes the whole frame, so output of a background job cannot split it. The directory is escaped
        // (% to %25, line break to %0A) so a line break in the path cannot add a line to the frame.
        return $". {ShellWords.Quote(commandFile)} </dev/null\n"
            + "__nooa_ec=$?\n"
            + "__nooa_dir=${PWD//\\%/%25}\n"
            + "__nooa_dir=${__nooa_dir//$'\\n'/%0A}\n"
            + $"builtin printf '\\n%s\\n%s\\n%s\\n%s\\n%s\\n' '{sentinel}' \"$__nooa_ec\" \"$__nooa_dir\" \"$(builtin jobs -p)\" '{sentinel}'\n"
            + $"builtin printf '\\n%s\\n' '{sentinel}' >&2\n";
    }

    private bool ShellIsAlive() => _process is { HasExited: false } && _stdin is not null && _stdout is not null && _stderr is not null;

    private async Task<BashCommandResult> ExecuteAsync(
        string command,
        string? stdin,
        TimeSpan timeout,
        Action<StreamName, string>? sink,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!ShellIsAlive())
            {
                _logger.LogWarning("bash is not running (exit code {ExitCode}). Restarting the session.", _process?.HasExited == true ? _process.ExitCode : null);
                await ResetCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            var sentinel = NewSentinel();
            var commandFile = Path.Combine(_scratchDirectory!, "command.sh");
            var stdinFile = Path.Combine(_scratchDirectory!, "stdin.txt");
            string text = command;
            if (stdin is not null)
            {
                await File.WriteAllTextAsync(stdinFile, stdin, _utf8, cancellationToken).ConfigureAwait(false);
                // A group, not a subshell, so cd and exports persist. The line break keeps a trailing comment from hiding the brace.
                text = $"{{ {command}\n}} < {ShellWords.Quote(stdinFile)}\n";
            }

            await File.WriteAllTextAsync(commandFile, text, _utf8, cancellationToken).ConfigureAwait(false);
            _stdout!.Begin(sentinel, sink is null ? null : chunk => sink(StreamName.Stdout, chunk));
            _stderr!.Begin(sentinel, sink is null ? null : chunk => sink(StreamName.Stderr, chunk));
            try
            {
                await _stdin!.WriteAsync(BuildProtocol(sentinel, commandFile)).ConfigureAwait(false);
                await _stdin.FlushAsync().ConfigureAwait(false);
                break;
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                if (attempt > 0)
                {
                    throw new ShellToolException("The bash session could not recover after a write error.", error);
                }

                _logger.LogWarning(error, "Writing to bash failed. Restarting the session.");
                await ResetCoreAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        try
        {
            return await CollectAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // The files can hold secrets from the command. Do not leave them on disk between commands.
            if (_scratchDirectory is not null)
            {
                TryDelete(Path.Combine(_scratchDirectory, "command.sh"));
                TryDelete(Path.Combine(_scratchDirectory, "stdin.txt"));
            }
        }
    }

    private async Task<BashCommandResult> CollectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var process = _process!;
        var stdoutCollector = _stdout!;
        var stderrCollector = _stderr!;
        var bothDone = Task.WhenAll(stdoutCollector.Done, stderrCollector.Done);
        var exited = process.WaitForExitAsync(CancellationToken.None);
        var timedOut = false;
        var cancelled = false;
        using (var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            wait.CancelAfter(timeout);
            try
            {
                await Task.WhenAny(bothDone, exited).WaitAsync(wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = cancellationToken.IsCancellationRequested;
                timedOut = !cancelled;
            }
        }

        if (!timedOut && !cancelled && !bothDone.IsCompleted)
        {
            // Bash ended. Give the pipes a moment to deliver what is left.
            await Task.WhenAny(bothDone, Task.Delay(300, CancellationToken.None)).ConfigureAwait(false);
        }

        if (timedOut || cancelled)
        {
            var recovered = await InterruptAndRecoverAsync().ConfigureAwait(false);
            if (!recovered)
            {
                _logger.LogWarning("The shell did not recover after a timeout. Resetting the session.");
            }

            var partial = Build(timedOut, recovered);
            if (!recovered)
            {
                await ResetCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return partial;
        }

        var result = Build(false, true);
        if (result.ExitCode == -1)
        {
            _logger.LogWarning("The control stream ended: bash died while it ran a command.");
        }

        return result;
    }

    private BashCommandResult Build(bool timedOut, bool framed)
    {
        var stdoutCollector = _stdout!;
        var stderrCollector = _stderr!;
        var stdout = stdoutCollector.Text.Trim();
        var stderr = stderrCollector.Text.Trim();
        var exitCode = -1;
        if (framed && stdoutCollector.Framed)
        {
            var lines = stdoutCollector.Payload.Split('\n');
            exitCode = int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : 0;
            if (lines.Length > 1 && DecodeDirectory(lines[1]) is ['/', ..] directory)
            {
                _cwd = directory;
            }

            if (!timedOut)
            {
                _backgroundPids = lines.Skip(2)
                    .Select(line => int.TryParse(line.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? pid : 0)
                    .Where(pid => pid > 0)
                    .ToHashSet();
            }
        }

        return new BashCommandResult(stdout, stderr, timedOut ? TimeoutExitCode : exitCode, timedOut);
    }

    // Ends the running command tree and waits for the frame. Earlier background jobs and their subtrees stay alive.
    private async Task<bool> InterruptAndRecoverAsync()
    {
        var process = _process;
        if (process is null || process.HasExited)
        {
            return false;
        }

        // Capture the descendants before TERM can orphan them.
        var targets = await ProcessTree.SnapshotAsync(process.Id, _backgroundPids).ConfigureAwait(false);
        if (!Signal(targets, PosixSignals.Terminate))
        {
            // A shell builtin can wait without any child command.
            PosixSignals.Send(process.Id, PosixSignals.Interrupt);
        }

        var recovered = await WaitForFrameAsync(_terminateGrace).ConfigureAwait(false);

        // Refresh the reachable descendants, and keep the orphaned original targets.
        foreach (var (pid, identity) in await ProcessTree.SnapshotAsync(process.Id, _backgroundPids).ConfigureAwait(false))
        {
            targets.TryAdd(pid, identity);
        }

        Signal(targets, PosixSignals.Kill);
        return recovered || await WaitForFrameAsync(_killGrace).ConfigureAwait(false);
    }

    private async Task<bool> WaitForFrameAsync(TimeSpan grace)
    {
        var stdoutCollector = _stdout!;
        var stderrCollector = _stderr!;
        try
        {
            await Task.WhenAll(stdoutCollector.Done, stderrCollector.Done).WaitAsync(grace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }

        return stdoutCollector.Framed && stderrCollector.Framed;
    }

    // Signals descendants before their parents. A Linux start time guards against a reused PID.
    private static bool Signal(Dictionary<int, string?> targets, int signal)
    {
        var sent = false;
        foreach (var (pid, identity) in targets.Reverse())
        {
            if (OperatingSystem.IsLinux() && (identity is null || ProcessTree.StartTime(pid) != identity))
            {
                continue;
            }

            sent |= PosixSignals.Send(pid, signal);
        }

        return sent;
    }

    private async Task ResetCoreAsync(CancellationToken cancellationToken)
    {
        var cwd = _cwd;
        await CloseCoreAsync().ConfigureAwait(false);
        _cwd = cwd;
        await StartCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CloseCoreAsync()
    {
        var process = _process;
        var stdoutPump = _stdoutPump;
        var stderrPump = _stderrPump;
        var keep = _keepBackgroundOnClose;
        _process = null;
        _stdin = null;
        _stdout = null;
        _stderr = null;
        _stdoutPump = null;
        _stderrPump = null;
        _started = false;
        _backgroundPids = new HashSet<int>();
        if (process is not null)
        {
            await EndProcessAsync(process, keep).ConfigureAwait(false);
            if (!keep)
            {
                // A background job can keep a pipe open. Do not wait for it.
                await Task.WhenAny(Task.WhenAll(stdoutPump ?? Task.CompletedTask, stderrPump ?? Task.CompletedTask), Task.Delay(_closeGrace)).ConfigureAwait(false);
                process.Dispose();
            }
            else
            {
                // The pumps keep reading the pipes, so a kept job does not get SIGPIPE while this process runs.
            }
        }

        if (_scratchDirectory is not null)
        {
            try
            {
                Directory.Delete(_scratchDirectory, recursive: true);
            }
            catch (IOException)
            {
                // The directory is private and small. A later start creates a new one.
            }

            _scratchDirectory = null;
        }
    }

    private static async Task EndProcessAsync(Process process, bool keepBackground)
    {
        if (process.HasExited)
        {
            return;
        }

        if (keepBackground)
        {
            // A non-interactive bash ends at EOF and leaves its jobs alone.
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // Bash already closed the pipe.
            }

            if (!await ExitsWithinAsync(process, _closeGrace).ConfigureAwait(false))
            {
                PosixSignals.Send(process.Id, PosixSignals.Kill);
                await ExitsWithinAsync(process, _closeGrace).ConfigureAwait(false);
            }

            return;
        }

        // The shell leads its own process group, so the group id is the shell PID.
        PosixSignals.SendToGroup(process.Id, PosixSignals.Terminate);
        await ExitsWithinAsync(process, _closeGrace).ConfigureAwait(false);

        // Jobs that ignore TERM end here. The group id stays reserved while a member lives.
        PosixSignals.SendToGroup(process.Id, PosixSignals.Kill);
        await ExitsWithinAsync(process, _closeGrace).ConfigureAwait(false);
    }

    private static async Task<bool> ExitsWithinAsync(Process process, TimeSpan limit)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(limit).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A later run overwrites the file, and close deletes the directory.
        }
    }
}
