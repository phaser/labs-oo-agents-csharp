// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_exporters.py

using System.Diagnostics;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class JsonlSpanExporterTests
{
    private static readonly Dictionary<string, object?> _resource = [];

    private static Activity Span(string? session)
    {
        var span = NooaTracing.Source.StartActivity("s")!;
        span.SetTag("session.id", session);
        span.Stop();
        return span;
    }

    [Fact]
    public void Export_RoutesEachSpanToTheFileOfItsSession()
    {
        using var capture = new SpanCapture();
        using var dir = new TempDirectory();
        var exporter = new JsonlSpanExporter(dir.Path);

        Assert.True(exporter.Export([Span("a"), Span("b"), Span("a")], _resource));

        var a = Assert.Single(File.ReadAllLines(dir.File("a.jsonl")));
        Assert.StartsWith("{\"resourceSpans\"", a, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.Json.Nodes.JsonNode.Parse(a)!["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]!.AsArray().Count);
        Assert.Single(File.ReadAllLines(dir.File("b.jsonl")));
    }

    [Fact]
    public void Export_SpanWithoutSession_GoesToATimestampFile()
    {
        using var capture = new SpanCapture();
        using var dir = new TempDirectory();
        var exporter = new JsonlSpanExporter(dir.Path);

        exporter.Export([Span(null)], _resource);

        Assert.NotNull(exporter.DefaultFile);
        Assert.Matches("^[0-9]{8}_[0-9]{6}\\.jsonl$", Path.GetFileName(exporter.DefaultFile!));
        Assert.Single(File.ReadAllLines(exporter.DefaultFile!));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData(".hidden")]
    [InlineData("name\n")]
    [InlineData("")]
    public void Export_UnsafeSessionId_NeverBecomesAFileName(string session)
    {
        using var capture = new SpanCapture();
        using var dir = new TempDirectory();
        var parent = Directory.GetParent(dir.Path)!.FullName;
        var exporter = new JsonlSpanExporter(dir.Path);

        exporter.Export([Span(session)], _resource);

        Assert.False(File.Exists(Path.Combine(parent, "escape.jsonl")));
        Assert.Equal(exporter.DefaultFile, Directory.GetFiles(dir.Path).Single());
    }

    [Fact]
    public void Export_AppendsToAnExistingFile()
    {
        using var capture = new SpanCapture();
        using var dir = new TempDirectory();
        var exporter = new JsonlSpanExporter(dir.Path);

        exporter.Export([Span("a")], _resource);
        new JsonlSpanExporter(dir.Path).Export([Span("a")], _resource);

        Assert.Equal(2, File.ReadAllLines(dir.File("a.jsonl")).Length);
    }

    [Fact]
    public void Constructor_SymlinkDirectory_IsRefused()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var target = new TempDirectory();
        using var holder = new TempDirectory();
        var link = holder.File("link");
        Directory.CreateSymbolicLink(link, target.Path);

        var error = Assert.Throws<IOException>(() => new JsonlSpanExporter(link));

        Assert.Contains("symbolic link", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(target.Path));
    }

    [Fact]
    public void Export_SymlinkFile_IsRefusedAndTheTargetIsNotWritten()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var capture = new SpanCapture();
        using var dir = new TempDirectory();
        using var outside = new TempDirectory();
        var victim = outside.File("victim.txt");
        File.WriteAllText(victim, "keep");
        File.CreateSymbolicLink(dir.File("a.jsonl"), victim);
        var exporter = new JsonlSpanExporter(dir.Path);

        var ok = exporter.Export([Span("a")], _resource);

        Assert.False(ok);
        Assert.Equal("keep", File.ReadAllText(victim));
    }

    [Fact]
    public void Export_OnUnix_CreatesOwnerOnlyDirectoryAndFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var capture = new SpanCapture();
        using var holder = new TempDirectory();
        var directory = holder.File("traces");
        var exporter = new JsonlSpanExporter(directory);

        exporter.Export([Span("a")], _resource);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(directory, "a.jsonl")));
    }

    [Fact]
    public void Constructor_WithoutDirectory_UsesTraceDirOrTheDefault()
    {
        using var holder = new TempDirectory();
        using var env = new EnvVars();
        env.Set("TRACE_DIR", holder.File("from-env"));

        var exporter = new JsonlSpanExporter();

        Assert.Equal(holder.File("from-env"), JsonlSpanExporter.DefaultDirectory());
        Assert.True(Directory.Exists(holder.File("from-env")));
        Assert.StartsWith("otlp-file:", exporter.Describe(), StringComparison.Ordinal);
        env.Set("TRACE_DIR", null);
        Assert.EndsWith("traces", JsonlSpanExporter.DefaultDirectory(), StringComparison.Ordinal);
    }

    [Fact]
    public void FlushAndShutdown_AreNoOps()
    {
        using var dir = new TempDirectory();
        var exporter = new JsonlSpanExporter(dir.Path);

        Assert.True(exporter.Flush(TimeSpan.FromSeconds(1)));
        exporter.Shutdown();
    }
}
