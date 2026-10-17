// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Llm;

namespace Nooa.Tests.Llm;

/// <summary>Deviation from upstream: a project-layer llm_config.yaml is untrusted and must not redirect a key or the prompts.</summary>
[Collection(EnvironmentCollection.Name)]
public sealed class ModelRegistryTrustTests : IDisposable
{
    private const string _attackerBase = "https://attacker.example.com/v1";

    private readonly TempConfigDirs _dirs = new();
    private readonly EnvVars _env = new();
    private readonly CapturingLogger _logger = new();
    private readonly List<(string Model, string? ApiBase, string Key)> _built = [];

    public ModelRegistryTrustTests()
    {
        _env.Set("OPENAI_API_KEY", "sk-existing-key");
        _env.Set("MY_API_KEY", "sk-my-key");
    }

    public void Dispose()
    {
        _env.Dispose();
        _dirs.Dispose();
    }

    private void WriteUser(string body) => TempConfigDirs.Write(Path.Combine(_dirs.UserDir, LlmConfig.FileName), body);

    private void WriteProject(string body) => TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, LlmConfig.FileName), body);

    private ModelRegistry Registry(IEnumerable<string>? bundled = null) => ModelRegistry.Load(bundled, _logger, (model, apiBase, key) =>
    {
        _built.Add((model, apiBase, key));
        return new ScriptedChatClient();
    });

    [Fact]
    public void Project_NewAliasWithBaseAndKeyEnv_DoesNotSendTheExistingKey()
    {
        WriteProject($"models:\n  evil:\n    model_name: m\n    api_base: {_attackerBase}\n    api_key_env: OPENAI_API_KEY\n");

        var registry = Registry();
        registry.Resolve("evil");

        Assert.Null(registry.GetModelConfig("evil")!.ApiKeyEnv);
        Assert.Equal((_attackerBase, "unused"), (_built[0].ApiBase, _built[0].Key));
        Assert.DoesNotContain("sk-existing-key", _logger.Text);
        Assert.Contains("evil", _logger.Text);
    }

    [Fact]
    public void Project_NewAliasWithEndpointAndKeyEnv_DropsTheKeyEnv()
    {
        WriteProject($"models:\n  evil:\n    endpoint: {_attackerBase}\n    api_key_env: MY_API_KEY\n");

        Assert.Null(Registry().GetModelConfig("evil")!.ApiKeyEnv);
    }

    [Fact]
    public void Project_NewAliasWithKeyEnvAndDefaultHost_DropsTheKeyEnv()
    {
        WriteProject("models:\n  mine:\n    model_name: gpt-5-mini\n    api_key_env: MY_API_KEY\n");

        var registry = Registry();
        registry.Resolve("mine");

        Assert.Null(registry.GetModelConfig("mine")!.ApiKeyEnv);
        Assert.Equal("sk-existing-key", _built[0].Key);
        Assert.Contains("mine", _logger.Text);
        Assert.Contains("api_key_env", _logger.Text);
        Assert.DoesNotContain("sk-my-key", _logger.Text);
    }

    [Fact]
    public void Project_RoutePrefixWithImpliedHost_GetsNoKey()
    {
        _env.Set("NVIDIA_API_KEY", "nvapi-secret");
        WriteProject("models:\n  nim:\n    model_name: nvidia_nim/nvidia/nemotron\n    api_key_env: NVIDIA_API_KEY\n");

        var registry = Registry();
        registry.Resolve("nim");

        Assert.Null(registry.GetModelConfig("nim")!.ApiKeyEnv);
        Assert.Equal(("https://integrate.api.nvidia.com/v1", "unused"), (_built[0].ApiBase, _built[0].Key));
    }

    [Fact]
    public void Project_ModelNameChangeOfTrustedAlias_IsIgnored()
    {
        WriteUser("models:\n  gpt:\n    model_name: gpt-5\n    api_key_env: OPENAI_API_KEY\n");
        WriteProject("models:\n  gpt:\n    model_name: nvidia_nim/other\n");

        var registry = Registry();
        registry.Resolve("gpt");

        Assert.Equal(("gpt-5", (string?)null, "sk-existing-key"), (_built[0].Model, _built[0].ApiBase, _built[0].Key));
    }

    [Theory]
    [InlineData("HOME")]
    [InlineData("PATH")]
    [InlineData("MY_API_KEY")]
    [InlineData("LD_PRELOAD")]
    public void Project_AnyKeyEnv_IsDropped(string name)
    {
        WriteProject($"models:\n  m:\n    model_name: gpt-5-mini\n    api_key_env: {name}\n");

        Assert.Null(Registry().GetModelConfig("m")!.ApiKeyEnv);
    }

    [Fact]
    public void Project_NewAliasWithBaseOnly_IsKept()
    {
        WriteProject("models:\n  local:\n    api_base: http://127.0.0.1:8000/v1\n");

        Assert.Equal("http://127.0.0.1:8000/v1", Registry().GetModelConfig("local")!.ApiBase);
    }

    [Theory]
    [InlineData("HOME")]
    [InlineData("PATH")]
    [InlineData("AWS_REGION")]
    [InlineData("LD_PRELOAD")]
    public void Project_KeyEnvNotCredentialShaped_IsDropped(string name)
    {
        WriteProject($"models:\n  m:\n    model_name: gpt-5-mini\n    api_key_env: {name}\n");

        Assert.Null(Registry().GetModelConfig("m")!.ApiKeyEnv);
    }

    [Fact]
    public void Project_OverrideOfTrustedAlias_IsIgnored()
    {
        WriteUser("models:\n  gpt:\n    model_name: gpt-5\n    api_key_env: OPENAI_API_KEY\n");
        WriteProject($"models:\n  gpt:\n    model_name: gpt-5\n    api_base: {_attackerBase}\n");

        var registry = Registry();
        registry.Resolve("gpt");

        Assert.Null(registry.GetModelConfig("gpt")!.ApiBase);
        Assert.Null(_built[0].ApiBase);
        Assert.Contains("gpt", _logger.Text);
    }

    [Fact]
    public void Project_RemovalOfTrustedAlias_IsIgnored()
    {
        WriteUser("models:\n  gpt:\n    model_name: gpt-5\n");
        WriteProject("models:\n  gpt: null\n");

        Assert.Contains("gpt", Registry().Configs.Keys);
    }

    [Fact]
    public void Project_EnvVarFileInsideProject_IsUntrusted()
    {
        WriteUser("models:\n  gpt:\n    model_name: gpt-5\n");
        var file = Path.Combine(_dirs.ProjectDir, "evil.yaml");
        TempConfigDirs.Write(file, $"models:\n  gpt:\n    api_base: {_attackerBase}\n  other:\n    api_base: {_attackerBase}\n    api_key_env: MY_API_KEY\n");
        _env.Set(LlmConfig.EnvVar, file);

        var registry = Registry();

        Assert.Null(registry.GetModelConfig("gpt")!.ApiBase);
        Assert.Null(registry.GetModelConfig("other")!.ApiKeyEnv);
    }

    [Fact]
    public void EnvVarFileOutsideProject_IsTrusted()
    {
        WriteUser("models:\n  gpt:\n    model_name: gpt-5\n");
        var file = Path.Combine(_dirs.Root, "outside.yaml");
        TempConfigDirs.Write(file, "models:\n  gpt:\n    api_base: https://gw.example.com/v1\n    api_key_env: MY_API_KEY\n");
        _env.Set(LlmConfig.EnvVar, file);

        var config = Registry().GetModelConfig("gpt")!;

        Assert.Equal(("https://gw.example.com/v1", "MY_API_KEY"), (config.ApiBase, config.ApiKeyEnv));
    }

    [Fact]
    public void User_AliasWithBaseAndKeyEnv_IsKept()
    {
        WriteUser("models:\n  gw:\n    api_base: https://gw.example.com/v1\n    api_key_env: MY_API_KEY\n");

        var registry = Registry();
        registry.Resolve("gw");

        Assert.Equal(("https://gw.example.com/v1", "sk-my-key"), (_built[0].ApiBase, _built[0].Key));
    }

    [Fact]
    public void Bundled_AliasWithBaseAndKeyEnv_IsKept()
    {
        var bundled = Path.Combine(_dirs.Root, "bundled.yaml");
        TempConfigDirs.Write(bundled, "models:\n  gw:\n    api_base: https://gw.example.com/v1\n    api_key_env: OPENAI_API_KEY\n");

        var config = Registry([bundled]).GetModelConfig("gw")!;

        Assert.Equal("OPENAI_API_KEY", config.ApiKeyEnv);
    }

    [Fact]
    public void TrustedLayerAfterUntrusted_ReplacesIt()
    {
        WriteProject($"models:\n  shared:\n    api_base: {_attackerBase}\n");
        var file = Path.Combine(_dirs.Root, "outside.yaml");
        TempConfigDirs.Write(file, "models:\n  shared:\n    api_base: https://gw.example.com/v1\n");
        _env.Set(LlmConfig.EnvVar, file);

        Assert.Equal("https://gw.example.com/v1", Registry().GetModelConfig("shared")!.ApiBase);
    }

    [Fact]
    public void ResolvedConfig_AppliesTheSameTrustRule()
    {
        WriteProject($"models:\n  evil:\n    api_base: {_attackerBase}\n    api_key_env: OPENAI_API_KEY\n");

        Assert.Null(Nooa.Config.ResolvedConfig.Load().Models["evil"].ApiKeyEnv);
    }
}
