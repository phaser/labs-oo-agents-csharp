// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/config/test_resolved_config.py

using System.Text.Json;
using Nooa.Config;
using Nooa.Llm;

namespace Nooa.Tests.Config;

[Collection(EnvironmentCollection.Name)]
public sealed class ResolvedConfigTests : IDisposable
{
    private readonly TempConfigDirs _dirs = new();

    public void Dispose() => _dirs.Dispose();

    private void WriteUser(string file, string body) => TempConfigDirs.Write(Path.Combine(_dirs.UserDir, file), body);

    [Fact]
    public void Load_NoFiles_IsEmpty()
    {
        var resolved = ResolvedConfig.Load();

        Assert.Empty(resolved.Models);
        Assert.Empty(resolved.Secrets.Env);
        Assert.Empty(resolved.Settings);
    }

    [Fact]
    public void Load_SecretValues_AreMaskedOnPrintAndSerialization()
    {
        WriteUser("secrets.yaml", "env:\n  NVIDIA_INTERNAL_API_KEY: sk-supersecret\n");

        var resolved = ResolvedConfig.Load();

        Assert.DoesNotContain("sk-supersecret", resolved.ToString());
        Assert.DoesNotContain("sk-supersecret", resolved.Secrets.Env["NVIDIA_INTERNAL_API_KEY"].ToString());
        Assert.DoesNotContain("sk-supersecret", JsonSerializer.Serialize(resolved.Secrets.Env));
        Assert.Contains("NVIDIA_INTERNAL_API_KEY", resolved.ToString());
    }

    [Fact]
    public void Load_Secret_PlaintextViaGetSecretValue()
    {
        WriteUser("secrets.yaml", "env:\n  K: realvalue\n");

        Assert.Equal("realvalue", ResolvedConfig.Load().Secrets.Env["K"].GetSecretValue());
    }

    [Fact]
    public void Load_Settings_AreCarriedAsDictionary()
    {
        WriteUser("settings.yaml", "tui:\n  default_model: foo\n");

        var tui = Assert.IsType<Dictionary<string, object?>>(ResolvedConfig.Load().Settings["tui"]);

        Assert.Equal("foo", tui["default_model"]);
    }

    [Fact]
    public void Load_Models_AreTyped()
    {
        WriteUser(LlmConfig.FileName, """
            models:
              m1:
                model_name: openai/x
                api_base: https://api.openai.com/v1
                api_key_env: OPENAI_API_KEY
            """);

        var model = ResolvedConfig.Load().Models["m1"];

        Assert.Equal("https://api.openai.com/v1", model.ApiBase);
        Assert.Equal("OPENAI_API_KEY", model.ApiKeyEnv);
    }

    [Fact]
    public void Load_Sources_ListWinningFiles()
    {
        var settings = Path.Combine(_dirs.UserDir, "settings.yaml");
        TempConfigDirs.Write(settings, "tui: {}\n");

        Assert.Contains(NooaPaths.Canonicalize(settings), ResolvedConfig.Load().Sources["settings"]);
    }

    [Fact]
    public void GetModelConfig_KnownAlias_ReturnsTypedConfig()
    {
        WriteUser(LlmConfig.FileName, "models:\n  a:\n    model_name: openai/m\n");

        var config = ModelRegistry.Load().GetModelConfig("a");

        Assert.Equal("openai/m", config?.ModelName);
    }

    [Fact]
    public void GetModelConfig_UnknownAlias_ReturnsNull()
    {
        Assert.Null(ModelRegistry.Load().GetModelConfig("does-not-exist"));
    }
}
