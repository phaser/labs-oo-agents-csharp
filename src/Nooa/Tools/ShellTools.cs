// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nooa.Tools;

/// <summary>
/// A persistent shell plus file operations, with search results that give editable <see cref="Match"/> anchors.
/// Use these methods for shell commands and file operations:
/// <list type="bullet">
/// <item><description><c>RunAsync(command, stdin, timeout, cwd)</c>: run a shell command. <c>cd</c> and exports persist.</description></item>
/// <item><description><c>ReadAsync(path, lines)</c>: view a file or a region as a <see cref="Match"/>.</description></item>
/// <item><description><c>ReplaceAsync(matchOrPath, ...)</c>: edit at a <see cref="Match"/> anchor, or by a unique string.</description></item>
/// <item><description><c>WriteFileAsync(path, content)</c>: create or overwrite a file.</description></item>
/// </list>
/// When <c>RunAsync</c> runs a plain search (grep, egrep or rg), the result prints the exact output of the command and also carries
/// <see cref="ShellResult.Matches"/>: anchors to pass to <c>ReplaceAsync</c>. The anchors are null when the command is not a verifiable
/// plain search: a pipe into sed, awk, cut or sort, a context, count, only-matching or files-only flag, PCRE, or grep without -n.
/// Pass a payload as <c>stdin</c> instead of quoting it in the command.
/// </summary>
/// <remarks>
/// <para>
/// <b>Security.</b> The shell and the file operations run as the host user, without a sandbox. The caller creates an instance on purpose.
/// There is no default instance. See <see cref="BashSession"/> for the environment that the shell gets.
/// </para>
/// <para>Ripgrep (<c>rg</c>) is optional. Without it <c>ShellResult.Matches</c> is always null.</para>
/// </remarks>
public sealed partial class ShellTools : IAsyncDisposable
{
    private const int _regexTimeoutMs = 1000;
    private const int _maxRipgrepChars = 8 * 1024 * 1024;
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _ripgrepTimeout = TimeSpan.FromSeconds(30);

    private readonly BashSession _session;
    private string _cwd;

    /// <summary>Creates the tools and a shell session. The shell starts on the first command.</summary>
    /// <exception cref="PlatformNotSupportedException">The platform is Windows.</exception>
    public ShellTools(ShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _session = new BashSession(options);
        _cwd = _session.Cwd;
    }

    /// <summary>The current directory of the shell. File operations resolve relative paths against it.</summary>
    public string Cwd => _cwd;

    /// <summary>The underlying bash session.</summary>
    public BashSession Session => _session;

    /// <summary>Runs a ripgrep search and returns its standard output and exit code, or null when ripgrep is missing or the run failed. A test replaces it.</summary>
    internal Func<IReadOnlyList<string>, CancellationToken, Task<(string Stdout, int ExitCode)?>> RipgrepRunner { get; set; } = null!;

    /// <summary>
    /// Runs a shell command in the persistent session. The shell directory and environment survive.
    /// </summary>
    /// <param name="command">The shell command.</param>
    /// <param name="stdin">Text for the stdin of the command. No quoting is needed.</param>
    /// <param name="timeout">The time limit. Null means 30 seconds.</param>
    /// <param name="cwd">Directory for this command only, relative to the shell directory or absolute. The shell returns to its own directory.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    public async Task<ShellResult> RunAsync(
        string command,
        string? stdin = null,
        TimeSpan? timeout = null,
        string? cwd = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _session.RunAsync(InDirectory(command, cwd), timeout ?? _defaultTimeout, stdin, cancellationToken).ConfigureAwait(false);
        _cwd = _session.Cwd;

        // Anchors come from the shell directory, so a search that ran elsewhere gets none instead of anchors for the wrong files.
        IReadOnlyList<Match>? matches = null;
        if (stdin is null && cwd is null && IsPureSearch(command))
        {
            matches = await HarvestMatchesAsync(command, result.Stdout, cancellationToken).ConfigureAwait(false);
        }

        return new ShellResult(result.Stdout, result.Stderr, result.ExitCode, matches, result.TimedOut);
    }

    /// <summary>
    /// Streams command output as it arrives and ends with a <see cref="StreamDone"/> that has the exit code. Runs in the persistent session like
    /// <see cref="RunAsync"/>. Chunks are not trimmed or bounded.
    /// </summary>
    /// <param name="command">The shell command.</param>
    /// <param name="stdin">Text for the stdin of the command.</param>
    /// <param name="timeout">The time limit. Null means 30 seconds.</param>
    /// <param name="cwd">As for <see cref="RunAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    public async IAsyncEnumerable<ShellStreamEvent> RunStreamAsync(
        string command,
        string? stdin = null,
        TimeSpan? timeout = null,
        string? cwd = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (var item in _session.RunStreamAsync(InDirectory(command, cwd), timeout ?? _defaultTimeout, stdin, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            _cwd = _session.Cwd;
        }
    }

    /// <summary>Ends the shell. The next command starts a new one.</summary>
    public Task CloseAsync() => _session.CloseAsync();

    /// <summary>Ends the shell for good.</summary>
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    /// <summary>Reads a file, or a line range, as a <see cref="Match"/>. Print the match to see numbered lines.</summary>
    /// <param name="path">The path, relative to the shell directory or absolute.</param>
    /// <param name="lines">An optional (start, end) range, 1-indexed and inclusive.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<Match> ReadAsync(string path, (int Start, int End)? lines = null, CancellationToken cancellationToken = default)
    {
        var resolved = ResolvePath(path);
        var content = await TextLines.ReadAsync(resolved, cancellationToken).ConfigureAwait(false);
        var all = TextLines.Split(content);
        if (lines is { } range)
        {
            var start = Math.Max(1, range.Start);
            var end = Math.Min(all.Count, range.End);
            var count = Math.Max(0, end - start + 1);
            return new Match(path, start, end, string.Concat(all.Skip(start - 1).Take(count)), resolved);
        }

        return new Match(path, 1, all.Count, content, resolved);
    }

    /// <summary>
    /// Replaces the line region of a <see cref="Match"/> with new text. The match replaces its whole region, not a substring.
    /// A stale or invalid anchor throws <see cref="StaleMatchException"/> and writes nothing. Anchors are never relocated.
    /// This check runs before the write. It is not a lock against other writers.
    /// </summary>
    /// <param name="target">The anchor from <see cref="ReadAsync"/> or from a search.</param>
    /// <param name="newText">The replacement text.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    public async Task<FileWrite> ReplaceAsync(Match target, string newText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(newText);
        var resolved = target.ResolvedPath;
        string content;
        try
        {
            content = await TextLines.ReadAsync(resolved, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            throw new StaleMatchException(target, null, "file no longer exists");
        }
        catch (DirectoryNotFoundException)
        {
            throw new StaleMatchException(target, null, "file no longer exists");
        }

        var all = TextLines.Split(content);

        // Compare at the captured range only. Never search for the text or move the anchor.
        var emptyFileAnchor = target.Start == 1 && target.End == 0 && target.Text.Length == 0 && content.Length == 0;
        if (!emptyFileAnchor)
        {
            if (target.Start < 1 || target.End < target.Start)
            {
                throw new StaleMatchException(target, null, "invalid captured line range");
            }

            var current = string.Concat(all.Skip(target.Start - 1).Take(target.End - target.Start + 1));
            if (target.End > all.Count)
            {
                throw new StaleMatchException(target, current, "captured line range is out of bounds");
            }

            if (current != target.Text)
            {
                throw new StaleMatchException(target, current, "captured text no longer matches");
            }
        }

        var before = all.Take(Math.Max(0, target.Start - 1));
        var after = all.Skip(target.End).ToList();

        // Keep the file newline-terminated when the region reaches the end and the file ended with a newline.
        var reachesEnd = target.End >= all.Count;
        if (newText.Length > 0 && !newText.EndsWith('\n') && (after.Count > 0 || (reachesEnd && content.EndsWith('\n'))))
        {
            newText += "\n";
        }

        await TextLines.WriteAsync(resolved, string.Concat(before) + newText + string.Concat(after), cancellationToken).ConfigureAwait(false);
        var diff = $"--- a/{target.Path}\n+++ b/{target.Path}\n@@ -{target.Start},{target.End - target.Start + 1} @@\n";
        return new FileWrite(target.Path, $"Edited {target.Path} (replaced lines {target.Start}-{target.End})", diff, newText);
    }

    /// <summary>
    /// Replaces text in a file. <paramref name="oldText"/> must match exactly once. An empty <paramref name="newText"/> deletes it.
    /// </summary>
    /// <param name="path">The path, relative to the shell directory or absolute.</param>
    /// <param name="oldText">The text to find.</param>
    /// <param name="newText">The replacement text.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    public async Task<FileWrite> ReplaceAsync(string path, string oldText, string newText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        var resolved = ResolvePath(path);
        var content = await TextLines.ReadAsync(resolved, cancellationToken).ConfigureAwait(false);
        var count = oldText.Length == 0 ? content.Length + 1 : CountOccurrences(content, oldText);
        if (count == 0)
        {
            throw new ShellToolException($"old text not found in {path}. It must match exactly once: check whitespace and indentation.");
        }

        if (count > 1)
        {
            throw new ShellToolException($"old text matched {count} times in {path}. It must match exactly once: add surrounding context to make it unique.");
        }

        var index = content.IndexOf(oldText, StringComparison.Ordinal);
        await TextLines.WriteAsync(resolved, string.Concat(content.AsSpan(0, index), newText, content.AsSpan(index + oldText.Length)), cancellationToken).ConfigureAwait(false);
        return new FileWrite(path, $"Edited {path}", $"--- a/{path}\n+++ b/{path}", newText);
    }

    /// <summary>
    /// Rejects the call <c>ReplaceAsync(match, old, new)</c>. A <see cref="Match"/> takes the full replacement text for its region.
    /// For an old-to-new replacement, use the path form. The method exists so a caller that mixes the two forms gets an instruction.
    /// </summary>
    /// <param name="target">The anchor.</param>
    /// <param name="oldText">Not used.</param>
    /// <param name="newText">Not used.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <exception cref="ShellToolException">Always. No file changes.</exception>
    public Task<FileWrite> ReplaceAsync(Match target, string oldText, string? newText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        _ = oldText;
        _ = newText;
        _ = cancellationToken;
        throw new ShellToolException(
            "ReplaceAsync(match, old, new) is ambiguous and no file was changed. "
            + "A Match takes only the full replacement text for its entire line region: ReplaceAsync(match, newText). "
            + "For an old -> new substring replacement, use the path form ReplaceAsync(path, old, new); "
            + "use match.ResolvedPath to keep the original file even if the shell directory changed. "
            + "This guard prevents silently overwriting the whole matched region (possibly the entire file) with the old text.");
    }

    /// <summary>Creates or overwrites a file. Parent directories are created. No shell quoting is needed.</summary>
    /// <param name="path">The path, relative to the shell directory or absolute.</param>
    /// <param name="content">The full file content.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task<FileWrite> WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        var resolved = ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
        await TextLines.WriteAsync(resolved, content, cancellationToken).ConfigureAwait(false);
        var lineCount = content.Count(c => c == '\n') + (content.Length > 0 && !content.EndsWith('\n') ? 1 : 0);
        return new FileWrite(path, $"Created {path} ({lineCount} lines)", newText: content);
    }

    /// <summary>True when the command is a pure search whose matches map one to one to lines. See <see cref="SearchCommand.IsPure"/>.</summary>
    private static bool IsPureSearch(string command)
    {
        try
        {
            return SearchCommand.IsPure(command);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    // Runs the command in the directory in the session's own shell, then goes back. Not a subshell, so exports persist.
    // CDPATH is cleared for the cd, so a relative directory is a child of the shell directory. If the cd fails, its error
    // and status are the result, and the command does not run. The line break before the brace keeps a trailing comment
    // in the command from hiding it.
    private static string InDirectory(string command, string? cwd)
    {
        if (cwd is null)
        {
            return command;
        }

        return "__nooa_back=$PWD; "
            + $"if CDPATH= cd -- {ShellWords.Quote(cwd)}; then {{ {command}\n}}; "
            + "__nooa_rc=$?; cd -- \"$__nooa_back\"; else __nooa_rc=$?; fi; "
            + "( exit $__nooa_rc )";
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private string ResolvePath(string path) => NooaPaths.Canonicalize(Path.GetFullPath(path, _cwd));

    // Runs the ripgrep equivalent of the search to read anchors. Returns a list only when ripgrep succeeds and its
    // (path, line) set is consistent with the lines that the command printed. On any doubt it returns null.
    internal async Task<List<Match>?> HarvestMatchesAsync(string command, string displayedStdout, CancellationToken cancellationToken)
    {
        SearchCommand.ParsedSearch? search;
        string reduced;
        bool endsInTailPipe;
        try
        {
            reduced = SearchCommand.ReduceToSearch(command);
            search = SearchCommand.Parse(reduced);
            endsInTailPipe = SearchCommand.EndsInTailPipe(command);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (search is null)
        {
            return null;
        }

        var args = new List<string> { "--json", "-n", "--no-ignore" };
        if (search.IgnoreCase)
        {
            args.Add("-i");
        }

        if (search.FixedStrings)
        {
            args.Add("-F");
        }

        args.Add("--");
        args.Add(search.Pattern);
        args.AddRange(search.Paths.Count > 0 ? search.Paths : ["."]);

        (string Stdout, int ExitCode)? output;
        try
        {
            output = await (RipgrepRunner ?? RunRipgrepAsync)(args, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or Win32Exception or InvalidOperationException)
        {
            return null;
        }

        // ripgrep exits 0 with matches and 1 without. Any other status (bad regex, failure) means no anchors.
        if (output is not { ExitCode: 0 or 1 } rg)
        {
            return null;
        }

        var anchors = ParseRipgrepAnchors(rg.Stdout);
        if (anchors is null)
        {
            return null;
        }

        var singleFile = search.Paths.Count == 1 && !search.Paths[0].EndsWith('/') ? search.Paths[0] : null;
        if (singleFile is not null && singleFile.StartsWith("./", StringComparison.Ordinal))
        {
            singleFile = singleFile[2..];
        }

        var displayed = DisplayedAnchorLines(displayedStdout, singleFile);
        if (displayed is null)
        {
            return null; // The output is not in a verifiable file:line: form.
        }

        // Normally the displayed anchors must equal the ripgrep anchors. A safe tail pipe cuts the display to a prefix or a suffix,
        // so then the displayed set may be a subset. Every displayed line is still a real match, so there are fewer anchors, not wrong ones.
        var anchorSet = anchors.ToHashSet();
        List<(string Path, int Line)> keep;
        if (endsInTailPipe)
        {
            if (!displayed.IsSubsetOf(anchorSet))
            {
                return null;
            }

            keep = anchors.Where(displayed.Contains).ToList();
        }
        else if (!anchorSet.SetEquals(displayed))
        {
            return null;
        }
        else
        {
            keep = anchors;
        }

        var cache = new Dictionary<string, (string Resolved, List<string> Lines)>();
        var matches = new List<Match>();
        foreach (var (path, line) in keep)
        {
            if (!cache.TryGetValue(path, out var file))
            {
                try
                {
                    var resolved = ResolvePath(path);
                    file = (resolved, TextLines.Split(await TextLines.ReadAsync(resolved, cancellationToken).ConfigureAwait(false)));
                    cache[path] = file;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
                {
                    return null;
                }
            }

            if (line < 1 || line > file.Lines.Count)
            {
                return null;
            }

            matches.Add(new Match(path, line, line, file.Lines[line - 1], file.Resolved));
        }

        return matches;
    }

    // Returns the (path, line) of each match event, or null when the stream is not valid JSON lines.
    private static List<(string Path, int Line)>? ParseRipgrepAnchors(string json)
    {
        var anchors = new List<(string, int)>();
        foreach (var raw in json.Split('\n'))
        {
            if (raw.Trim().Length == 0)
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(raw);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || type.GetString() != "match")
                {
                    continue;
                }

                var data = root.GetProperty("data");

                // A path that is not valid UTF-8 has "bytes" and no "text". Such a path has no anchor.
                if (!data.GetProperty("path").TryGetProperty("text", out var textElement) || textElement.GetString() is not { } path)
                {
                    return null;
                }

                if (path.StartsWith("./", StringComparison.Ordinal))
                {
                    path = path[2..];
                }

                anchors.Add((path, data.GetProperty("line_number").GetInt32()));
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                return null; // A malformed stream: do not guess.
            }
        }

        return anchors;
    }

    // Parses path:line: from the command's own output. Returns null when the output is not in that form.
    // grep omits the file name for a single file ("line:text"). Then every line belongs to singleFile.
    private static HashSet<(string Path, int Line)>? DisplayedAnchorLines(string stdout, string? singleFile)
    {
        var found = new HashSet<(string, int)>();
        var anyLine = false;
        foreach (var line in stdout.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.Length == 0 && !anyLine && stdout.Length == 0)
            {
                break;
            }

            anyLine = true;
            try
            {
                var match = PathLineAnchor().Match(line);
                if (match.Success)
                {
                    found.Add((match.Groups[1].Value, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
                }
                else if (singleFile is not null && LineOnlyAnchor().Match(line) is { Success: true } lineOnly)
                {
                    found.Add((singleFile, int.Parse(lineOnly.Groups[1].Value, CultureInfo.InvariantCulture)));
                }
            }
            catch (Exception error) when (error is RegexMatchTimeoutException or OverflowException)
            {
                return null;
            }
        }

        if (!anyLine)
        {
            return []; // No output means no matches, and that is verifiable.
        }

        return found.Count == 0 ? null : found;
    }

    private async Task<(string Stdout, int ExitCode)?> RunRipgrepAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("rg")
        {
            WorkingDirectory = _cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        // A clean environment: no ripgrep config file and no host secret reaches the search.
        info.Environment.Clear();
        info.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
        Process process;
        try
        {
            process = Process.Start(info) ?? throw new InvalidOperationException("rg did not start.");
        }
        catch (Win32Exception)
        {
            return null; // ripgrep is not installed.
        }

        using (process)
        using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            limit.CancelAfter(_ripgrepTimeout);
            try
            {
                var errors = process.StandardError.ReadToEndAsync(limit.Token);
                var output = new StringBuilder();
                var buffer = new char[8192];
                int read;
                while ((read = await process.StandardOutput.ReadAsync(buffer.AsMemory(), limit.Token).ConfigureAwait(false)) > 0)
                {
                    output.Append(buffer, 0, read);
                    if (output.Length > _maxRipgrepChars)
                    {
                        process.Kill(entireProcessTree: true);
                        return null;
                    }
                }

                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
                await errors.ConfigureAwait(false);
                return (output.ToString(), process.ExitCode);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
        }
    }

    [GeneratedRegex(@"^(?:\./)?([^:]+):(\d+):", RegexOptions.None, _regexTimeoutMs)]
    private static partial Regex PathLineAnchor();

    [GeneratedRegex(@"^(\d+):", RegexOptions.None, _regexTimeoutMs)]
    private static partial Regex LineOnlyAnchor();
}
