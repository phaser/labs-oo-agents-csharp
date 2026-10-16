// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

[Collection(EnvironmentCollection.Name)]
public sealed class NooaPathsTests : IDisposable
{
    private readonly TempConfigDirs _dirs = new();

    public void Dispose() => _dirs.Dispose();

    [Fact]
    public void GetUserDir_UserDirOverride_IsUsedAsBase()
    {
        Assert.Equal(_dirs.UserDir, NooaPaths.GetUserDir());
        Assert.Equal(Path.Combine(_dirs.UserDir, "sessions", "a"), NooaPaths.GetUserDir("sessions", "a"));
    }

    [Fact]
    public void GetUserDir_XdgConfigHome_UsedWhenNoOverride()
    {
        Environment.SetEnvironmentVariable(NooaPaths.UserDirEnvVar, null);
        var xdg = Path.Combine(_dirs.Root, "xdg");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdg);

        Assert.Equal(Path.Combine(xdg, "nooa", "secrets.yaml"), NooaPaths.GetUserDir("secrets.yaml"));
    }

    [Fact]
    public void GetUserDir_NoEnvironment_UsesHomeConfig()
    {
        Environment.SetEnvironmentVariable(NooaPaths.UserDirEnvVar, null);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.Combine(home, ".config", "nooa"), NooaPaths.GetUserDir());
    }

    [Fact]
    public void GetProjectDir_ProjectDirOverride_IsUsedAsBase()
    {
        Assert.Equal(_dirs.ProjectDir, NooaPaths.GetProjectDir());
        Assert.Equal(Path.Combine(_dirs.ProjectDir, "settings.yaml"), NooaPaths.GetProjectDir("settings.yaml"));
    }

    [Fact]
    public void GetProjectDir_NoOverride_UsesNooaDirUnderProjectRoot()
    {
        Environment.SetEnvironmentVariable(NooaPaths.ProjectDirEnvVar, null);

        Assert.Equal(NooaPaths.DirName, Path.GetFileName(NooaPaths.GetProjectDir()));
        Assert.Equal(NooaPaths.FindProjectRoot(), Path.GetDirectoryName(NooaPaths.GetProjectDir()));
    }

    [Fact]
    public void FindProjectRoot_NooaDirInAncestor_ReturnsThatAncestor()
    {
        var root = Path.Combine(_dirs.Root, "repo");
        Directory.CreateDirectory(Path.Combine(root, ".nooa"));
        var deep = Directory.CreateDirectory(Path.Combine(root, "a", "b")).FullName;

        Assert.Equal(root, NooaPaths.FindProjectRoot(deep));
    }

    [Fact]
    public void FindProjectRoot_GitEntryInAncestor_ReturnsThatAncestor()
    {
        var root = Path.Combine(_dirs.Root, "repo");
        TempConfigDirs.Write(Path.Combine(root, ".git"), "gitdir: elsewhere");
        var deep = Directory.CreateDirectory(Path.Combine(root, "a")).FullName;

        Assert.Equal(root, NooaPaths.FindProjectRoot(deep));
    }

    [Fact]
    public void FindProjectRoot_NoMarker_FallsBackToStartDirectory()
    {
        var start = Directory.CreateDirectory(Path.Combine(_dirs.Root, "plain")).FullName;

        Assert.Equal(start, NooaPaths.FindProjectRoot(start));
    }

    [Fact]
    public void Canonicalize_SymlinkedDirectoryComponent_IsResolved()
    {
        var real = Directory.CreateDirectory(Path.Combine(_dirs.Root, "real")).FullName;
        File.WriteAllText(Path.Combine(real, "f.yaml"), "a: 1\n");
        var link = Path.Combine(_dirs.Root, "link");
        Directory.CreateSymbolicLink(link, real);

        Assert.Equal(
            NooaPaths.Canonicalize(Path.Combine(real, "f.yaml")),
            NooaPaths.Canonicalize(Path.Combine(link, "f.yaml")));
    }

    [Fact]
    public void Canonicalize_MissingPath_IsReturnedAbsolute()
    {
        var missing = Path.Combine(_dirs.Root, "nope", "x.yaml");

        Assert.EndsWith(Path.Combine("nope", "x.yaml"), NooaPaths.Canonicalize(missing));
    }

    [Fact]
    public void PathEquals_DifferentCase_FollowsPlatformRule()
    {
        var caseInsensitive = OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

        Assert.Equal(caseInsensitive, NooaPaths.PathEquals("/a/Secrets.yaml", "/a/secrets.yaml"));
    }

    [Fact]
    public void IsInside_DifferentCase_FollowsPlatformRule()
    {
        var caseInsensitive = OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();
        var sep = Path.DirectorySeparatorChar;

        Assert.Equal(caseInsensitive, NooaPaths.IsInside($"{sep}Proj{sep}x.yaml", $"{sep}proj"));
    }

    [Fact]
    public void IsInside_SiblingWithSamePrefix_IsFalse()
    {
        var sep = Path.DirectorySeparatorChar;

        Assert.False(NooaPaths.IsInside($"{sep}project2{sep}x.yaml", $"{sep}project"));
    }
}
