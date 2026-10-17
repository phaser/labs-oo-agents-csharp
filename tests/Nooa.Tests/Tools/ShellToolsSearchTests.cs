// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tools/test_shell_tools_modern.py (the search anchors of ShellTools.RunAsync)

using System.Diagnostics;
using System.Text.Json;
using Nooa.Tools;

namespace Nooa.Tests.Tools;

/// <summary>
/// Differential oracle: for a pure search, the anchors must equal the (path, line) set that the grep of the command printed.
/// On any divergence the matches must be null (fail closed). Tests that need ripgrep skip when it is missing.
/// </summary>
public sealed class ShellToolsSearchTests : IAsyncLifetime
{
    private static readonly string[] _scrapedPatterns =
    [
        "Cycle", "FILE_UPLOAD_PERMISSION", "FILE_UPLOAD_PERMISSIONS", "GenericForeignKey", "InlineModelAdmin", "Max", "RenameContentType",
        "TIME_ZONE", "URLValidator", "UUIDField", "ValueError", "W0611", "_chain", "_eval_evalf", "_imp_", "COUNT.*DISTINCT", "Count.*Case",
        "Count.*Case.*distinct", "\\.evalf\\(", "connection.timezone", "def test.*hist", "def test.*model_to_dict", "def test.*union",
        "empty.*fields", "hist.*range.*density", "hist_kwargs.*=.*dict", "inspect.isclass", "integrate.*dim", "integrate.*dim=",
        "method.*integrate", "^def method|^    def method", "^import|^from.*import", "combined_queries|combinator", "cotm|cothm",
        "def hstack|def vstack", "def set_xlim|def set_ylim", "def set_ylim|def set_xlim", "empty.*name|name.*empty", "hstack|vstack",
        "prepend|append", "set_xlim|set_ylim", "set_ylim|set_xlim", "set_ylim|set_xlim|invert", "test.*delete|test_delete", "timezone_name|timezone",
    ];

    private readonly ShellTestDirectory _directory = new();
    private ShellTools? _shell;

    public static TheoryData<string> ScrapedPatterns => [.. _scrapedPatterns];

    public static TheoryData<string> ScrapedAlternations => [.. _scrapedPatterns.Where(p => p.Contains('|', StringComparison.Ordinal))];

    public static TheoryData<int> Seeds => [.. Enumerable.Range(0, 40)];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_shell is not null)
        {
            await _shell.DisposeAsync();
        }

        _directory.Dispose();
    }

    private static bool Has(string tool)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator).Any(dir => File.Exists(Path.Combine(dir, tool)));
    }

    private static void RequireRipgrep()
    {
        if (!Has("rg") || !Has("grep"))
        {
            Assert.Skip("needs rg and grep on PATH");
        }
    }

    private ShellTools Shell(string? cwd = null)
    {
        _shell = new ShellTools(new ShellOptions { Cwd = cwd ?? _directory.Path });
        return _shell;
    }

    private void SeedRepo()
    {
        File.WriteAllText(_directory.File("a.py"), "def foo():\n    return \"bar\"\n# foo again\nfooo = 1\n");
        File.WriteAllText(_directory.File("b.txt"), "no match here\nFOO upper\n  foo indented\n");
        Directory.CreateDirectory(_directory.File("sub"));
        File.WriteAllText(_directory.File("sub/c.py"), "x = 1\nfoo = 2\n");
    }

    private static HashSet<(string Path, int Line)> GrepAnchors(string directory, string command)
    {
        var info = new ProcessStartInfo("/bin/bash") { WorkingDirectory = directory, RedirectStandardOutput = true, UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(command);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var anchors = new HashSet<(string, int)>();
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split(':', 3);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var number) && parts[1].All(char.IsAsciiDigit))
            {
                anchors.Add((parts[0].StartsWith("./", StringComparison.Ordinal) ? parts[0][2..] : parts[0], number));
            }
        }

        return anchors;
    }

    private static HashSet<(string Path, int Start)> Anchors(IReadOnlyList<Match> matches) => matches.Select(m => (m.Path, m.Start)).ToHashSet();

    [Theory]
    [InlineData("grep -rn 'foo' .")]
    [InlineData("grep -rni 'foo' .")]
    [InlineData("rg -n 'foo' .")]
    [InlineData("grep -rn 'def ' .")]
    [InlineData("grep -rn 'nomatch_zzz' .")]
    public async Task RunAsync_PureSearch_MatchesEqualGrep(string command)
    {
        RequireRipgrep();
        SeedRepo();

        var result = await Shell().RunAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result.Matches);
        Assert.Equal(GrepAnchors(_directory.Path, command), Anchors(result.Matches));
    }

    [Fact]
    public async Task ReplaceAsync_SearchMatch_EditsTheAnchor()
    {
        RequireRipgrep();
        SeedRepo();
        var shell = Shell();

        var result = await shell.RunAsync("grep -rn 'fooo = 1' .", cancellationToken: TestContext.Current.CancellationToken);
        var match = Assert.Single(result.Matches!, m => m.Path == "a.py");
        await shell.ReplaceAsync(match, "fooo = 999\n", TestContext.Current.CancellationToken);

        Assert.Contains("fooo = 999", File.ReadAllText(_directory.File("a.py")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestMatchesAsync_RepeatedNoncontiguousPath_KeepsEachResolvedPath()
    {
        SeedRepo();
        var shell = Shell();
        shell.RipgrepRunner = (_, _) =>
        {
            var records = new[] { "a.py", "b.txt", "a.py" }.Select(path => JsonSerializer.Serialize(new { type = "match", data = new { path = new { text = path }, line_number = 1 } }));
            return Task.FromResult<(string, int)?>((string.Join("\n", records), 0));
        };

        var matches = await shell.HarvestMatchesAsync("grep -rn 'match' .", "a.py:1:match\nb.txt:1:match\na.py:1:match\n", TestContext.Current.CancellationToken);

        Assert.NotNull(matches);
        Assert.Equal(["a.py", "b.txt", "a.py"], matches.Select(m => m.Path));
        Assert.Equal([_directory.File("a.py"), _directory.File("b.txt"), _directory.File("a.py")], matches.Select(m => m.ResolvedPath));
    }

    [Fact]
    public async Task RunAsync_SearchMatch_StaysBoundAfterCwdChange()
    {
        RequireRipgrep();
        SeedRepo();
        Directory.CreateDirectory(_directory.File("other"));
        File.WriteAllText(_directory.File("other/a.py"), "fooo = 1\n");
        var shell = Shell();

        var result = await shell.RunAsync("grep -n 'fooo = 1' a.py", cancellationToken: TestContext.Current.CancellationToken);
        var match = Assert.Single(result.Matches!);
        await shell.RunAsync("cd other", cancellationToken: TestContext.Current.CancellationToken);
        await shell.ReplaceAsync(match, "fooo = 999\n", TestContext.Current.CancellationToken);

        Assert.Contains("fooo = 999", File.ReadAllText(_directory.File("a.py")), StringComparison.Ordinal);
        Assert.Equal("fooo = 1\n", File.ReadAllText(_directory.File("other/a.py")));
    }

    [Theory]
    [InlineData("grep -rno 'foo' .")]
    [InlineData("grep -rnA1 'foo' .")]
    [InlineData("cat a.py")]
    [InlineData("grep -rnP 'fo+' .")]
    public async Task RunAsync_NotAPureSearch_AttachesNothing(string command)
    {
        SeedRepo();

        var result = await Shell().RunAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result.Matches);
    }

    [Fact]
    public async Task RunAsync_GrepWithoutLineNumbers_IsUnverifiable()
    {
        RequireRipgrep();
        SeedRepo();

        var result = await Shell().RunAsync("grep -r 'foo' .", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result.Matches);
    }

    [Fact]
    public async Task RunAsync_SearchWithCwdOrStdin_AttachesNothing()
    {
        RequireRipgrep();
        SeedRepo();
        var shell = Shell();

        Assert.Null((await shell.RunAsync("grep -rn foo .", cwd: "sub", cancellationToken: TestContext.Current.CancellationToken)).Matches);
        Assert.Null((await shell.RunAsync("grep -rn foo .", stdin: "x", cancellationToken: TestContext.Current.CancellationToken)).Matches);
    }

    [Fact]
    public async Task RunAsync_HeadPipe_AttachesTheDisplayedSubset()
    {
        RequireRipgrep();
        SeedRepo();
        var shell = Shell();
        var full = await shell.RunAsync("grep -rn 'foo' .", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(full.Matches is { Count: > 1 });

        var result = await shell.RunAsync("grep -rn 'foo' . | head -1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result.Matches);
        var displayed = new HashSet<(string, int)>();
        foreach (var line in result.Stdout.Split('\n'))
        {
            var parts = line.Split(':', 3);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var number))
            {
                displayed.Add((parts[0].StartsWith("./", StringComparison.Ordinal) ? parts[0][2..] : parts[0], number));
            }
        }

        Assert.Equal(displayed, Anchors(result.Matches));
        Assert.Subset(Anchors(full.Matches), Anchors(result.Matches));
    }

    [Fact]
    public async Task ReplaceAsync_HeadPipeMatch_EditsTheCorrectAnchor()
    {
        RequireRipgrep();
        SeedRepo();
        var shell = Shell();

        var result = await shell.RunAsync("grep -rn 'fooo = 1' . | head -5", cancellationToken: TestContext.Current.CancellationToken);
        var match = Assert.Single(result.Matches!, m => m.Path == "a.py");
        await shell.ReplaceAsync(match, "fooo = 777\n", TestContext.Current.CancellationToken);

        Assert.Contains("fooo = 777", File.ReadAllText(_directory.File("a.py")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("grep -n 'foo' a.py")]
    [InlineData("grep -rn 'foo' a.py")]
    public async Task RunAsync_SingleFileGrep_AttachesMatchesOfThatFile(string command)
    {
        RequireRipgrep();
        SeedRepo();

        var result = await Shell().RunAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result.Matches);
        Assert.Equal(GrepAnchors(_directory.Path, command.Replace(" a.py", " --with-filename a.py", StringComparison.Ordinal)), Anchors(result.Matches));
        Assert.All(result.Matches, m => Assert.Equal("a.py", m.Path));
    }

    [Fact]
    public async Task RunAsync_MissingRipgrep_FailsClosed()
    {
        SeedRepo();
        var shell = Shell();
        shell.RipgrepRunner = (_, _) => Task.FromResult<(string, int)?>(null);

        var result = await shell.RunAsync("grep -rn 'foo' .", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result.Matches);
        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("not json", 0)]
    [InlineData("", 2)]
    public async Task HarvestMatchesAsync_BadRipgrepOutput_ReturnsNull(string stdout, int exitCode)
    {
        SeedRepo();
        var shell = Shell();
        shell.RipgrepRunner = (_, _) => Task.FromResult<(string, int)?>((stdout, exitCode));

        Assert.Null(await shell.HarvestMatchesAsync("grep -rn 'foo' .", "a.py:1:foo\n", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HarvestMatchesAsync_PatternAndPathsReachRipgrepAsArguments()
    {
        SeedRepo();
        var shell = Shell();
        IReadOnlyList<string>? seen = null;
        shell.RipgrepRunner = (args, _) =>
        {
            seen = args;
            return Task.FromResult<(string, int)?>(("", 1));
        };

        await shell.HarvestMatchesAsync("grep -rni '$(touch x); -e' sub", "", TestContext.Current.CancellationToken);

        Assert.Equal(["--json", "-n", "--no-ignore", "-i", "--", "$(touch x); -e", "sub"], seen);
        Assert.False(File.Exists(_directory.File("x")));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task RunAsync_RandomCorpus_AnchorsEqualGrepOrAreNull(int seed)
    {
        RequireRipgrep();
        var root = Directory.CreateDirectory(_directory.File($"c{seed}")).FullName;
        var random = new Random(seed);
        const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ  \t:#.()[]λ";
        for (var i = 0; i < random.Next(1, 5); i++)
        {
            var lines = Enumerable.Range(0, random.Next(0, 7)).Select(_ => new string(Enumerable.Range(0, random.Next(0, 31)).Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray())).ToList();
            File.WriteAllText(Path.Combine(root, $"f{i}.txt"), string.Join("\n", lines) + (lines.Count > 0 ? "\n" : string.Empty));
        }

        var pattern = new[] { "a", "b", "abc", "x", ":", "la", "f" }[new Random(seed * 7919).Next(7)];
        var command = $"grep -rn -F '{pattern}' .";

        var result = await Shell(root).RunAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        if (result.Matches is not null)
        {
            Assert.Equal(GrepAnchors(root, command), Anchors(result.Matches));
        }
    }

    private void SeedCodeRepo()
    {
        File.WriteAllText(
            _directory.File("models.py"),
            "class URLValidator:\n    pass\ndef _chain(self):\n    return self\nCOUNT = 1  # COUNT DISTINCT\nFILE_UPLOAD_PERMISSIONS = 0o644\nimport os\nfrom django.db import connection\n");
        Directory.CreateDirectory(_directory.File("tests"));
        File.WriteAllText(
            _directory.File("tests/test_x.py"),
            "def test_union(self):\n    pass\ndef test_delete(self):\n    pass\ndef set_xlim(self):\n    pass\ndef set_ylim(self):\n    pass\n");
    }

    [Theory]
    [MemberData(nameof(ScrapedPatterns))]
    public async Task RunAsync_ScrapedPatterns_AnchorsEqualGrepOrAreNull(string pattern)
    {
        RequireRipgrep();
        SeedCodeRepo();
        var command = $"grep -rn '{pattern.Replace("'", "'\\''", StringComparison.Ordinal)}' .";

        var result = await Shell().RunAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        var truth = GrepAnchors(_directory.Path, command);
        if (result.Matches is not null)
        {
            Assert.Equal(truth, Anchors(result.Matches));
        }
        else
        {
            Assert.Empty(truth);
        }
    }

    [Theory]
    [MemberData(nameof(ScrapedAlternations))]
    public async Task RunAsync_AlternationPatterns_PipeInPatternIsNotAShellPipe(string pattern)
    {
        RequireRipgrep();
        SeedCodeRepo();
        var command = $"grep -rn '{pattern}' .";

        Assert.True(SearchCommand.IsPure(command));
        var result = await Shell().RunAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        if (result.Matches is not null)
        {
            Assert.Equal(GrepAnchors(_directory.Path, command), Anchors(result.Matches));
        }
    }
}
