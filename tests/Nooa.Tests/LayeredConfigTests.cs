// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_layered_config.py

using Microsoft.Extensions.Logging;

namespace Nooa.Tests;

[Collection(EnvironmentCollection.Name)]
public sealed class LayeredConfigTests : IDisposable
{
    private const string _envVar = "NEMO_OO_SETTINGS";
    private const string _filename = "settings.yaml";

    private readonly TempConfigDirs _dirs = new();

    public void Dispose() => _dirs.Dispose();

    private static string Resolved(string path) => NooaPaths.Canonicalize(path);

    // ── LayeredPaths ──

    [Fact]
    public void LayeredPaths_NoFiles_IsEmpty()
    {
        Assert.Empty(LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_UserOnly_ReturnsUserFile()
    {
        var p = Path.Combine(_dirs.UserDir, _filename);
        TempConfigDirs.Write(p, "a: 1\n");

        Assert.Equal([Resolved(p)], LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_UserAndProject_UserFirst()
    {
        var u = Path.Combine(_dirs.UserDir, _filename);
        var pr = Path.Combine(_dirs.ProjectDir, _filename);
        TempConfigDirs.Write(u, "a: 1\n");
        TempConfigDirs.Write(pr, "a: 2\n");

        Assert.Equal([Resolved(u), Resolved(pr)], LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_ExplicitProjectDir_IsIndependentOfProcessConfig()
    {
        var processProject = Path.Combine(_dirs.ProjectDir, _filename);
        var workspaceConfig = Path.Combine(_dirs.Root, "workspace", ".nooa");
        var workspaceProject = Path.Combine(workspaceConfig, _filename);
        TempConfigDirs.Write(processProject, "source: process\n");
        TempConfigDirs.Write(workspaceProject, "source: workspace\n");

        Assert.Equal([Resolved(workspaceProject)], LayeredConfig.LayeredPaths(_filename, _envVar, projectDir: workspaceConfig));
    }

    [Fact]
    public void LayeredPaths_EnvVar_HasHighestPriority()
    {
        var u = Path.Combine(_dirs.UserDir, _filename);
        var pr = Path.Combine(_dirs.ProjectDir, _filename);
        var e = Path.Combine(_dirs.Root, "e.yaml");
        foreach (var x in new[] { u, pr, e })
        {
            TempConfigDirs.Write(x, "a: 1\n");
        }

        Environment.SetEnvironmentVariable(_envVar, e);

        Assert.Equal([Resolved(u), Resolved(pr), Resolved(e)], LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_EnvVarWithSeveralPaths_KeepsListOrder()
    {
        var e1 = Path.Combine(_dirs.Root, "e1.yaml");
        var e2 = Path.Combine(_dirs.Root, "e2.yaml");
        TempConfigDirs.Write(e1, "a: 1\n");
        TempConfigDirs.Write(e2, "a: 2\n");
        Environment.SetEnvironmentVariable(_envVar, $" {e1} , ,{e2} ");

        Assert.Equal([Resolved(e1), Resolved(e2)], LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_Prepend_IsLowestPriority()
    {
        var bundled = Path.Combine(_dirs.Root, "bundled.yaml");
        var u = Path.Combine(_dirs.UserDir, _filename);
        TempConfigDirs.Write(bundled, "a: 1\n");
        TempConfigDirs.Write(u, "a: 2\n");

        var chain = LayeredConfig.LayeredPaths(_filename, _envVar, prepend: [bundled, Path.Combine(_dirs.Root, "missing.yaml")]);

        Assert.Equal([Resolved(bundled), Resolved(u)], chain);
    }

    [Fact]
    public void LayeredPaths_NullEnvVar_DisablesEnvLayer()
    {
        var e = Path.Combine(_dirs.Root, "e.yaml");
        TempConfigDirs.Write(e, "a: 1\n");
        Environment.SetEnvironmentVariable(_envVar, e);

        Assert.Empty(LayeredConfig.LayeredPaths(_filename, null));
    }

    [Fact]
    public void LayeredPaths_SamePathInTwoLayers_KeepsHigherPriorityPosition()
    {
        var u = Path.Combine(_dirs.UserDir, _filename);
        TempConfigDirs.Write(u, "a: 1\n");
        Environment.SetEnvironmentVariable(_envVar, u);

        Assert.Equal([Resolved(u)], LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_SymlinkAlias_CollapsesToHigherPriorityEntry()
    {
        var u = Path.Combine(_dirs.UserDir, _filename);
        var pr = Path.Combine(_dirs.ProjectDir, _filename);
        var alias = Path.Combine(_dirs.Root, "alias.yaml");
        TempConfigDirs.Write(u, "a: 1\n");
        TempConfigDirs.Write(pr, "a: 2\n");
        File.CreateSymbolicLink(alias, u);
        Environment.SetEnvironmentVariable(_envVar, alias);

        Assert.Equal([Resolved(pr), Resolved(u)], LayeredConfig.LayeredPaths(_filename, _envVar));
    }

    [Fact]
    public void LayeredPaths_MissingEnvPath_WarnsAndSkips()
    {
        var logger = new CapturingLogger();
        Environment.SetEnvironmentVariable(_envVar, "/nope/nope.yaml");

        Assert.Empty(LayeredConfig.LayeredPaths(_filename, _envVar, logger: logger));

        Assert.Contains("does not exist", logger.Text);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    // ── LoadLayeredYaml ──

    [Fact]
    public void LoadLayeredYaml_NoFiles_ReturnsEmptyDictionary()
    {
        Assert.Empty(LayeredConfig.LoadLayeredYaml(_filename, _envVar));
    }

    [Fact]
    public void LoadLayeredYaml_SingleLayer_ReturnsTypedScalars()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\nvi: true\ncount: 3\nratio: 0.5\n");

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar);

        Assert.Equal("foo", merged["model"]);
        Assert.Equal(true, merged["vi"]);
        Assert.Equal(3, Convert.ToInt32(merged["count"]));
        Assert.Equal(0.5, Convert.ToDouble(merged["ratio"]));
    }

    [Fact]
    public void LoadLayeredYaml_QuotedScalar_StaysString()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "code: \"12345\"\n");

        Assert.Equal("12345", LayeredConfig.LoadLayeredYaml(_filename, _envVar)["code"]);
    }

    [Fact]
    public void LoadLayeredYaml_Sequence_BecomesList()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "items:\n  - a\n  - b\n");

        var items = Assert.IsType<List<object?>>(LayeredConfig.LoadLayeredYaml(_filename, _envVar)["items"]);

        Assert.Equal(new object?[] { "a", "b" }, items);
    }

    [Fact]
    public void LoadLayeredYaml_TwoLayers_LastWinsScalar()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), "model: bar\n");

        Assert.Equal("bar", LayeredConfig.LoadLayeredYaml(_filename, _envVar)["model"]);
    }

    [Fact]
    public void LoadLayeredYaml_ExplicitProjectDir_IsLoaded()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), "model: process\n");
        var workspaceConfig = Path.Combine(_dirs.Root, "workspace", ".nooa");
        TempConfigDirs.Write(Path.Combine(workspaceConfig, _filename), "model: workspace\n");

        Assert.Equal("workspace", LayeredConfig.LoadLayeredYaml(_filename, _envVar, projectDir: workspaceConfig)["model"]);
    }

    [Fact]
    public void LoadLayeredYaml_NestedMappings_DeepMerge()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "summarization:\n  policy: token_budget\n  preserve_recent: 50\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), "summarization:\n  preserve_recent: 99\n");

        var summarization = Assert.IsType<Dictionary<string, object?>>(LayeredConfig.LoadLayeredYaml(_filename, _envVar)["summarization"]);

        Assert.Equal("token_budget", summarization["policy"]);
        Assert.Equal(99, Convert.ToInt32(summarization["preserve_recent"]));
    }

    [Fact]
    public void LoadLayeredYaml_NullValue_DeletesKey()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\nvi: true\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), "vi: null\n");

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar);

        Assert.Equal(["model"], merged.Keys);
    }

    [Fact]
    public void LoadLayeredYaml_NonMappingFile_IsSkippedWithWarning()
    {
        var logger = new CapturingLogger();
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), "- just\n- a\n- list\n");

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar, logger: logger);

        Assert.Equal(["model"], merged.Keys);
        Assert.Contains("not a YAML mapping", logger.Text);
    }

    [Fact]
    public void LoadLayeredYaml_EmptyFile_IsSkipped()
    {
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), string.Empty);

        Assert.Equal(["model"], LayeredConfig.LoadLayeredYaml(_filename, _envVar).Keys);
    }

    [Fact]
    public void LoadLayeredYaml_InvalidYaml_IsSkippedWithoutQuotingContent()
    {
        var logger = new CapturingLogger();
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, _filename), "key: [PRIVATE-VALUE\n");

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar, logger: logger);

        Assert.Equal(["model"], merged.Keys);
        Assert.Contains("Failed to load config file", logger.Text);
        Assert.DoesNotContain("PRIVATE", logger.Text);
    }

    [Fact]
    public void LoadLayeredYaml_InvalidUtf8_IsSkippedWithWarning()
    {
        var logger = new CapturingLogger();
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\n");
        File.WriteAllBytes(Path.Combine(_dirs.ProjectDir, _filename), [0xFF, 0xFE, 0x80, 0x0A]);

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar, logger: logger);

        Assert.Equal(["model"], merged.Keys);
        Assert.Contains("Failed to load config file", logger.Text);
    }

    [Fact]
    public void LoadLayeredYaml_Prepend_IsMergedFirst()
    {
        var bundled = Path.Combine(_dirs.Root, "bundled.yaml");
        TempConfigDirs.Write(bundled, "model: bundled\nonly_bundled: yes\n");
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: user\n");

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar, prepend: [bundled]);

        Assert.Equal("user", merged["model"]);
        Assert.True(merged.ContainsKey("only_bundled"));
    }

    // ── Untrusted YAML limits (deviation from upstream) ──

    [Theory]
    [InlineData("a: &x 1\nb: *x\n")]
    [InlineData("a: &x 1\n")]
    [InlineData("a: &x [1, 2]\nb: [*x, *x]\n")]
    public void ParseYaml_AnchorOrAlias_Throws(string yaml)
    {
        Assert.Throws<InvalidDataException>(() => LayeredConfig.ParseYaml(yaml));
    }

    [Fact]
    public void ParseYaml_BillionLaughs_IsRejectedBeforeExpansion()
    {
        const string Bomb = "a: &a [x, x, x, x, x, x, x, x, x]\nb: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a]\nc: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b]\nd: [*c, *c, *c, *c, *c, *c, *c, *c, *c]\n";

        Assert.Throws<InvalidDataException>(() => LayeredConfig.ParseYaml(Bomb));
    }

    [Fact]
    public void ParseYaml_NestingAtLimit_IsAccepted()
    {
        Assert.NotNull(LayeredConfig.ParseYaml(new string('[', 64) + new string(']', 64)));
    }

    [Fact]
    public void ParseYaml_NestingPastLimit_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => LayeredConfig.ParseYaml(new string('[', 65) + new string(']', 65)));

        Assert.Contains("64", ex.Message);
    }

    [Fact]
    public void LoadLayeredYaml_FileWithAlias_IsSkippedWithWarningNamingFile()
    {
        var logger = new CapturingLogger();
        TempConfigDirs.Write(Path.Combine(_dirs.UserDir, _filename), "model: foo\n");
        var bad = Path.Combine(_dirs.ProjectDir, _filename);
        TempConfigDirs.Write(bad, "a: &x PRIVATE\nb: *x\n");

        var merged = LayeredConfig.LoadLayeredYaml(_filename, _envVar, logger: logger);

        Assert.Equal(["model"], merged.Keys);
        Assert.Contains(_filename, logger.Text);
        Assert.Contains("are not allowed", logger.Text);
        Assert.DoesNotContain("PRIVATE", logger.Text);
    }
}
