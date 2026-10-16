// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

[Collection(EnvironmentCollection.Name)]
public sealed class LlmConfigTests : IDisposable
{
    private readonly TempConfigDirs _dirs = new();

    public void Dispose() => _dirs.Dispose();

    [Fact]
    public void Chain_NoFiles_IsEmpty()
    {
        Assert.Empty(LlmConfig.Chain());
    }

    [Fact]
    public void Chain_AllLayers_OrderedLowestPriorityFirst()
    {
        var bundled = Path.Combine(_dirs.Root, "bundled.yaml");
        var user = Path.Combine(_dirs.UserDir, LlmConfig.FileName);
        var project = Path.Combine(_dirs.ProjectDir, LlmConfig.FileName);
        var env = Path.Combine(_dirs.Root, "env.yaml");
        foreach (var p in new[] { bundled, user, project, env })
        {
            TempConfigDirs.Write(p, "aliases: {}\n");
        }

        Environment.SetEnvironmentVariable(LlmConfig.EnvVar, env);

        var chain = LlmConfig.Chain([bundled]);

        Assert.Equal(
            new[] { bundled, user, project, env }.Select(NooaPaths.Canonicalize),
            chain);
    }

    [Fact]
    public void Chain_MissingBundledPath_IsDropped()
    {
        var user = Path.Combine(_dirs.UserDir, LlmConfig.FileName);
        TempConfigDirs.Write(user, "aliases: {}\n");

        Assert.Equal([NooaPaths.Canonicalize(user)], LlmConfig.Chain([Path.Combine(_dirs.Root, "gone.yaml")]));
    }

    [Fact]
    public void Chain_EnvVarMissingFile_WarnsAndSkips()
    {
        var logger = new CapturingLogger();
        Environment.SetEnvironmentVariable(LlmConfig.EnvVar, "/nope/llm.yaml");

        Assert.Empty(LlmConfig.Chain(logger: logger));

        Assert.Contains(LlmConfig.EnvVar, logger.Text);
    }
}
