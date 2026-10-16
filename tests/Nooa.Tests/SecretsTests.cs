// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_secrets.py

using System.Runtime.Versioning;

namespace Nooa.Tests;

[Collection(EnvironmentCollection.Name)]
public sealed class SecretsTests : IDisposable
{
    private const string _key = "NEMO_TEST_SECRET_KEY";
    private const string _key2 = "NEMO_TEST_SECRET_KEY2";

    private readonly TempConfigDirs _dirs = new();

    public SecretsTests()
    {
        Environment.SetEnvironmentVariable(_key, null);
        Environment.SetEnvironmentVariable(_key2, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_key, null);
        Environment.SetEnvironmentVariable(_key2, null);
        _dirs.Dispose();
    }

    private void WriteUser(string body) => TempConfigDirs.Write(Path.Combine(_dirs.UserDir, Secrets.FileName), body);

    private void WriteProject(string body) => TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, Secrets.FileName), body);

    // ── LoadSecretsIntoEnv ──

    [Fact]
    public void LoadSecretsIntoEnv_NoFile_IsNoop()
    {
        Assert.Empty(Secrets.LoadSecretsIntoEnv());
    }

    [Fact]
    public void LoadSecretsIntoEnv_EnvMap_SetsVariables()
    {
        WriteUser($"env:\n  {_key}: sk-123\n");

        var applied = Secrets.LoadSecretsIntoEnv();

        Assert.Equal([_key], applied);
        Assert.Equal("sk-123", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_ExistingVariable_IsNotOverwritten()
    {
        Environment.SetEnvironmentVariable(_key, "from-shell");
        WriteUser($"env:\n  {_key}: from-file\n");

        var applied = Secrets.LoadSecretsIntoEnv();

        Assert.Empty(applied);
        Assert.Equal("from-shell", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_ProjectAndUserSameName_UserWins()
    {
        // Deviation from upstream (project wins): an untrusted project file must not swap the user's credential.
        WriteUser($"env:\n  {_key}: user-val\n");
        WriteProject($"env:\n  {_key}: project-val\n");

        Secrets.LoadSecretsIntoEnv();

        Assert.Equal("user-val", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_ProjectNullValue_DoesNotDeleteUserValue()
    {
        WriteUser($"env:\n  {_key}: user-val\n");
        WriteProject($"env:\n  {_key}: null\n");

        Secrets.LoadSecretsIntoEnv();

        Assert.Equal("user-val", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_ProjectOnlyCredential_IsAccepted()
    {
        WriteProject($"env:\n  {_key}: project-val\n");

        Secrets.LoadSecretsIntoEnv();

        Assert.Equal("project-val", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_CalledTwice_SecondCallAppliesNothing()
    {
        WriteUser($"env:\n  {_key}: sk-123\n");

        Assert.Equal([_key], Secrets.LoadSecretsIntoEnv());
        Assert.Empty(Secrets.LoadSecretsIntoEnv());
        Assert.Equal("sk-123", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_NumericValue_IsConvertedToText()
    {
        WriteUser($"env:\n  {_key}: 12345\n");

        Secrets.LoadSecretsIntoEnv();

        Assert.Equal("12345", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_NullValue_IsSkipped()
    {
        WriteUser($"env:\n  {_key}: sk-123\n  {_key2}: null\n");

        var applied = Secrets.LoadSecretsIntoEnv();

        Assert.Equal([_key], applied);
        Assert.Null(Environment.GetEnvironmentVariable(_key2));
    }

    [Fact]
    public void LoadSecretsIntoEnv_EnvVarOverrideLayer_Wins()
    {
        WriteUser($"env:\n  {_key}: user-val\n");
        var overridePath = Path.Combine(_dirs.Root, "override.yaml");
        TempConfigDirs.Write(overridePath, $"env:\n  {_key}: override-val\n");
        Environment.SetEnvironmentVariable(Secrets.EnvVar, overridePath);

        Secrets.LoadSecretsIntoEnv();

        Assert.Equal("override-val", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_NonMappingEnv_WarnsAndIgnores()
    {
        var logger = new CapturingLogger();
        WriteUser("env:\n  - just\n  - a list\n");

        Assert.Empty(Secrets.LoadSecretsIntoEnv(logger));

        Assert.Contains("not a mapping", logger.Text);
    }

    [Fact]
    public void LoadSecretsIntoEnv_Logs_NeverIncludeSecretValues()
    {
        var logger = new CapturingLogger();
        WriteUser($"env:\n  {_key}: PRIVATE-VALUE\n");
        WriteProject("env: [PRIVATE-BROKEN\n");

        Secrets.LoadSecretsIntoEnv(logger);

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain("PRIVATE", logger.Text);
    }

    // ── WriteSecretEnv ──

    [Fact]
    public void WriteSecretEnv_ExistingFile_PreservesValuesAndDoesNotExport()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");
        File.WriteAllText(path, "env:\n  OTHER: old-value\nmetadata: keep\n");

        Secrets.WriteSecretEnv(path, _key, "new-value");

        var data = Assert.IsType<Dictionary<string, object?>>(LayeredConfig.ParseYaml(File.ReadAllText(path)));
        var env = Assert.IsType<Dictionary<string, object?>>(data["env"]);
        Assert.Equal("old-value", env["OTHER"]);
        Assert.Equal("new-value", env[_key]);
        Assert.Equal("keep", data["metadata"]);
        Assert.Null(Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void WriteSecretEnv_NewFile_IsOwnerReadWriteOnly()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only.");
        var path = Path.Combine(_dirs.Root, "sub", "secrets.yaml");

        Secrets.WriteSecretEnv(path, _key, "new-value");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".*.tmp"));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void WriteSecretEnv_ExistingFile_KeepsOwnerOnlyMode()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only.");
        var path = Path.Combine(_dirs.Root, "secrets.yaml");
        File.WriteAllText(path, "env: {}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        Secrets.WriteSecretEnv(path, _key, "new-value");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void WriteSecretEnv_NullEnvKey_IsReplacedByMapping()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");
        File.WriteAllText(path, "env:\n");

        Secrets.WriteSecretEnv(path, _key, "v");

        var data = Assert.IsType<Dictionary<string, object?>>(LayeredConfig.ParseYaml(File.ReadAllText(path)));
        Assert.Equal("v", Assert.IsType<Dictionary<string, object?>>(data["env"])[_key]);
    }

    [Fact]
    public void WriteSecretEnv_InvalidYaml_IsNotDisclosedOrReplaced()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");
        const string Original = "env: [PRIVATE-OLD-KEY";
        File.WriteAllText(path, Original);

        var ex = Assert.Throws<InvalidDataException>(() => Secrets.WriteSecretEnv(path, _key, "PRIVATE-NEW-KEY"));

        Assert.DoesNotContain("PRIVATE", ex.Message);
        Assert.Null(ex.InnerException);
        Assert.Equal(Original, File.ReadAllText(path));
    }

    [Fact]
    public void WriteSecretEnv_EnvNotMapping_Throws()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");
        File.WriteAllText(path, "env:\n  - a\n");

        var ex = Assert.Throws<InvalidDataException>(() => Secrets.WriteSecretEnv(path, _key, "v"));

        Assert.Contains("env mapping", ex.Message);
    }

    [Fact]
    public void WriteSecretEnv_TopLevelNotMapping_Throws()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");
        File.WriteAllText(path, "- a\n");

        Assert.Throws<InvalidDataException>(() => Secrets.WriteSecretEnv(path, _key, "v"));
    }

    [Theory]
    [InlineData("1BAD")]
    [InlineData("has space")]
    [InlineData("")]
    [InlineData("a-b")]
    public void WriteSecretEnv_InvalidName_Throws(string name)
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");

        Assert.Throws<ArgumentException>(() => Secrets.WriteSecretEnv(path, name, "v"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void WriteSecretEnv_EmptyValue_Throws()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");

        Assert.Throws<ArgumentException>(() => Secrets.WriteSecretEnv(path, _key, string.Empty));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void WriteSecretEnv_AmbiguousValue_RoundTripsAsText()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");

        Secrets.WriteSecretEnv(path, _key, "true");
        Secrets.WriteSecretEnv(path, _key2, "0123");
        Environment.SetEnvironmentVariable(Secrets.EnvVar, path);
        Secrets.LoadSecretsIntoEnv();

        Assert.Equal("true", Environment.GetEnvironmentVariable(_key));
        Assert.Equal("0123", Environment.GetEnvironmentVariable(_key2));
    }

    // ── Untrusted file protections (deviation from upstream) ──

    [Theory]
    [InlineData("LD_PRELOAD")]
    [InlineData("DYLD_INSERT_LIBRARIES")]
    [InlineData("PATH")]
    [InlineData("path")]
    [InlineData("BASH_ENV")]
    [InlineData("DOTNET_STARTUP_HOOKS")]
    [InlineData("COMPlus_EnableDiagnostics")]
    [InlineData("HTTPS_PROXY")]
    [InlineData("OPENAI_BASE_URL")]
    [InlineData("AZURE_OPENAI_ENDPOINT")]
    [InlineData("ANTHROPIC_API_BASE")]
    [InlineData("NODE_OPTIONS")]
    public void LoadSecretsIntoEnv_DeniedName_IsRefusedAndLoggedWithoutValue(string name)
    {
        var logger = new CapturingLogger();
        Environment.SetEnvironmentVariable(name, null);
        WriteUser($"env:\n  {name}: PRIVATE-VALUE\n  {_key}: ok\n");

        var applied = Secrets.LoadSecretsIntoEnv(logger);

        Assert.Equal([_key], applied);
        Assert.Null(Environment.GetEnvironmentVariable(name));
        Assert.Contains(name, logger.Text);
        Assert.DoesNotContain("PRIVATE", logger.Text);
    }

    [Theory]
    [InlineData("1BAD")]
    [InlineData("has space")]
    [InlineData("a-b")]
    public void LoadSecretsIntoEnv_InvalidName_IsRefused(string name)
    {
        var logger = new CapturingLogger();
        WriteUser($"env:\n  \"{name}\": PRIVATE-VALUE\n");

        Assert.Empty(Secrets.LoadSecretsIntoEnv(logger));

        Assert.Contains("not a valid environment variable name", logger.Text);
        Assert.DoesNotContain("PRIVATE", logger.Text);
    }

    [Fact]
    public void LoadSecretsIntoEnv_ProjectLayerBaseUrl_DoesNotRedirectExistingKey()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "real-key");
        try
        {
            WriteProject("env:\n  OPENAI_BASE_URL: https://attacker.example\n");

            Secrets.LoadSecretsIntoEnv();

            Assert.Null(Environment.GetEnvironmentVariable("OPENAI_BASE_URL"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        }
    }

    [Fact]
    public void WriteSecretEnv_DeniedName_Throws()
    {
        var path = Path.Combine(_dirs.Root, "secrets.yaml");

        Assert.Throws<ArgumentException>(() => Secrets.WriteSecretEnv(path, "LD_PRELOAD", "v"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void LoadSecretsIntoEnv_AliasInSecretsFile_IsSkipped()
    {
        WriteUser($"env:\n  {_key}: &x PRIVATE\n  {_key2}: *x\n");

        Assert.Empty(Secrets.LoadSecretsIntoEnv());
    }

    // ── Layer-aware trust ──

    [Fact]
    public void LoadSecretsIntoEnv_ProjectLayerNonCredentialName_IsRefused()
    {
        var logger = new CapturingLogger();
        WriteProject("env:\n  MY_SETTING: PRIVATE-VALUE\n  MY_API_KEY: PRIVATE-KEY\n");
        Environment.SetEnvironmentVariable("MY_SETTING", null);
        Environment.SetEnvironmentVariable("MY_API_KEY", null);
        try
        {
            var applied = Secrets.LoadSecretsIntoEnv(logger);

            Assert.Equal(["MY_API_KEY"], applied);
            Assert.Null(Environment.GetEnvironmentVariable("MY_SETTING"));
            Assert.Contains("MY_SETTING", logger.Text);
            Assert.Contains("project-layer", logger.Text);
            Assert.DoesNotContain("PRIVATE", logger.Text);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MY_API_KEY", null);
        }
    }

    [Theory]
    [InlineData("OPENAI_API_KEY")]
    [InlineData("SERVICE_KEY")]
    [InlineData("gh_token")]
    [InlineData("APP_SECRET")]
    [InlineData("DB_PASSWORD")]
    public void LoadSecretsIntoEnv_ProjectLayerCredentialName_IsAccepted(string name)
    {
        Environment.SetEnvironmentVariable(name, null);
        WriteProject($"env:\n  {name}: v\n");
        try
        {
            Assert.Equal([name], Secrets.LoadSecretsIntoEnv());
            Assert.Equal("v", Environment.GetEnvironmentVariable(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void LoadSecretsIntoEnv_UserLayerNonCredentialName_IsAccepted()
    {
        Environment.SetEnvironmentVariable("MY_SETTING", null);
        WriteUser("env:\n  MY_SETTING: v\n");
        try
        {
            Assert.Equal(["MY_SETTING"], Secrets.LoadSecretsIntoEnv());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MY_SETTING", null);
        }
    }

    [Fact]
    public void LoadSecretsIntoEnv_EnvVarFileInsideProjectDir_IsUntrusted()
    {
        var inside = Path.Combine(_dirs.ProjectDir, "extra.yaml");
        TempConfigDirs.Write(inside, "env:\n  MY_SETTING: v\n");
        Environment.SetEnvironmentVariable(Secrets.EnvVar, inside);
        Environment.SetEnvironmentVariable("MY_SETTING", null);

        Assert.Empty(Secrets.LoadSecretsIntoEnv());
    }

    [Fact]
    public void LoadSecretsIntoEnv_EnvVarFileOutsideProject_IsTrusted()
    {
        var outside = Path.Combine(_dirs.Root, "outside.yaml");
        TempConfigDirs.Write(outside, "env:\n  MY_SETTING: v\n");
        Environment.SetEnvironmentVariable(Secrets.EnvVar, outside);
        Environment.SetEnvironmentVariable("MY_SETTING", null);
        try
        {
            Assert.Equal(["MY_SETTING"], Secrets.LoadSecretsIntoEnv());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MY_SETTING", null);
        }
    }

    [Theory]
    [InlineData("NEMO_OO_USER_DIR")]
    [InlineData("NOOA_X_TOKEN")]
    [InlineData("XDG_CONFIG_HOME")]
    [InlineData("GIT_SSH_COMMAND")]
    [InlineData("SSH_AUTH_SOCK")]
    [InlineData("NPM_CONFIG_REGISTRY")]
    [InlineData("PIP_INDEX_URL")]
    [InlineData("EDITOR")]
    [InlineData("ZDOTDIR")]
    [InlineData("NODE_PATH")]
    [InlineData("SERVICE_HOST")]
    [InlineData("SERVICE_URL")]
    [InlineData("LD_PRELOAD")]
    public void LoadSecretsIntoEnv_DeniedName_IsRefusedInEveryLayer(string name)
    {
        var original = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, null);
        try
        {
            WriteUser($"env:\n  {name}: v\n");
            WriteProject($"env:\n  {name}: v\n");
            var outside = Path.Combine(_dirs.Root, "outside.yaml");
            TempConfigDirs.Write(outside, $"env:\n  {name}: v\n");
            Environment.SetEnvironmentVariable(Secrets.EnvVar, outside);

            Assert.Empty(Secrets.LoadSecretsIntoEnv());
            Assert.Null(Environment.GetEnvironmentVariable(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }

    [Fact]
    public void LoadSecretsIntoEnv_ProjectLayerKey_NeverOverridesExistingEnv()
    {
        Environment.SetEnvironmentVariable(_key, "from-shell");
        WriteProject($"env:\n  {_key}: from-project\n");

        Assert.Empty(Secrets.LoadSecretsIntoEnv());
        Assert.Equal("from-shell", Environment.GetEnvironmentVariable(_key));
    }

    [Fact]
    public void LoadSecretsIntoEnv_RefusedProjectNameDoesNotOverrideUserValue()
    {
        Environment.SetEnvironmentVariable("MY_SETTING", null);
        WriteUser("env:\n  MY_SETTING: trusted\n");
        WriteProject("env:\n  MY_SETTING: attacker\n");
        try
        {
            Secrets.LoadSecretsIntoEnv();

            Assert.Equal("trusted", Environment.GetEnvironmentVariable("MY_SETTING"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MY_SETTING", null);
        }
    }
}
