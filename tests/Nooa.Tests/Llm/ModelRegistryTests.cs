// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_model_registry.py

using Microsoft.Extensions.AI;
using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

/// <summary>
/// Upstream tests write their files to the project dir. Here the user dir is the trusted layer,
/// so each test writes to the user dir unless it tests the project layer.
/// </summary>
[Collection(EnvironmentCollection.Name)]
public sealed class ModelRegistryTests : IDisposable
{
    private readonly TempConfigDirs _dirs = new();
    private readonly EnvVars _env = new();
    private readonly CapturingLogger _logger = new();
    private readonly List<(string Model, string? ApiBase, string Key)> _built = [];
    private readonly ScriptedChatClient _chat = new();

    public void Dispose()
    {
        _env.Dispose();
        _dirs.Dispose();
    }

    private void WriteUser(string body) => TempConfigDirs.Write(Path.Combine(_dirs.UserDir, LlmConfig.FileName), body);

    private ModelRegistry Registry() => ModelRegistry.Load(null, _logger, (model, apiBase, key) =>
    {
        _built.Add((model, apiBase, key));
        return _chat;
    });

    [Fact]
    public void Load_NoFiles_RegistryIsEmpty()
    {
        Assert.Empty(Registry().Configs);
    }

    [Fact]
    public void Resolve_UnknownAlias_ThrowsKeyNotFound()
    {
        var e = Assert.Throws<KeyNotFoundException>(() => Registry().Resolve("gpt-4o-mini"));

        Assert.Contains("gpt-4o-mini", e.Message);
    }

    [Fact]
    public void Resolve_RegistryModel_UsesModelNameWithoutRoutePrefix()
    {
        WriteUser("models:\n  my-alias:\n    model_name: openai/my-org/my-model\n    api_base: https://gw.example.com/v1\n");

        Registry().Resolve("my-alias");

        Assert.Equal(("my-org/my-model", "https://gw.example.com/v1"), (_built[0].Model, _built[0].ApiBase));
    }

    [Fact]
    public void Resolve_NoModelName_UsesAlias()
    {
        WriteUser("models:\n  plain:\n    api_base: http://127.0.0.1:8000/v1\n");

        Registry().Resolve("plain");

        Assert.Equal("plain", _built[0].Model);
    }

    [Fact]
    public void Resolve_UnknownRoutePrefix_StaysInTheName()
    {
        WriteUser("models:\n  m:\n    model_name: nvidia/nemotron\n    api_base: https://gw.example.com/v1\n");

        Registry().Resolve("m");

        Assert.Equal("nvidia/nemotron", _built[0].Model);
    }

    [Fact]
    public void Resolve_NvidiaNimModel_UsesNimBase()
    {
        WriteUser("models:\n  nim:\n    model_name: nvidia_nim/nvidia/nemotron\n    api_key_env: NVIDIA_API_KEY\n");
        _env.Set("NVIDIA_API_KEY", "k");

        Registry().Resolve("nim");

        Assert.Equal(("nvidia/nemotron", "https://integrate.api.nvidia.com/v1"), (_built[0].Model, _built[0].ApiBase));
    }

    [Fact]
    public void Resolve_ContextWindow_ReachesTheClient()
    {
        WriteUser("models:\n  m:\n    api_base: http://127.0.0.1:1/v1\n    context_window: 128000\n");

        Assert.Equal(128000, Registry().Resolve("m").ContextWindow);
    }

    [Fact]
    public void Resolve_ApiKeyFromEnv_IsPassedToTheFactory()
    {
        WriteUser("models:\n  keyed:\n    model_name: test-model\n    api_key_env: MY_TEST_KEY\n");
        _env.Set("MY_TEST_KEY", "test-key-abc");

        Registry().Resolve("keyed");

        Assert.Equal("test-key-abc", _built[0].Key);
    }

    [Fact]
    public void Resolve_MissingKeyEnv_FailsWithoutFallbackKey()
    {
        WriteUser("models:\n  keyed:\n    model_name: test-model\n    api_base: https://gw.example.com/v1\n    api_key_env: NONEXISTENT_KEY\n");
        _env.Set("NONEXISTENT_KEY", null);
        _env.Set("OPENAI_API_KEY", "sk-must-not-be-sent");

        var e = Assert.Throws<ConfigValidationException>(() => Registry().Resolve("keyed"));

        Assert.Contains("NONEXISTENT_KEY", e.Message);
        Assert.DoesNotContain("sk-must-not-be-sent", e.Message);
        Assert.Empty(_built);
    }

    [Theory]
    [InlineData("NVIDIA_INTERNAL_API_KEY", null, "NVIDIA_INFERENCE_API_KEY", "new-name-key", "new-name-key")]
    [InlineData("NVIDIA_INFERENCE_API_KEY", null, "NVIDIA_INTERNAL_API_KEY", "old-name-key", "old-name-key")]
    [InlineData("NVIDIA_INFERENCE_API_KEY", "declared-key", "NVIDIA_INTERNAL_API_KEY", "synonym-key", "declared-key")]
    public void Resolve_NvidiaKeySynonym_Works(string declared, string? declaredValue, string synonym, string synonymValue, string expected)
    {
        WriteUser($"models:\n  n:\n    model_name: test-model\n    api_key_env: {declared}\n");
        _env.Set(declared, declaredValue);
        _env.Set(synonym, synonymValue);

        Registry().Resolve("n");

        Assert.Equal(expected, _built[0].Key);
    }

    [Fact]
    public void Resolve_NoKeyEnvAndNoBase_UsesOpenAiKey()
    {
        WriteUser("models:\n  m:\n    model_name: gpt-5-mini\n");
        _env.Set("OPENAI_API_KEY", "sk-openai");

        Registry().Resolve("m");

        Assert.Equal(("gpt-5-mini", null, "sk-openai"), (_built[0].Model, _built[0].ApiBase, _built[0].Key));
    }

    [Fact]
    public void Resolve_NoKeyEnvAndNoBaseAndNoOpenAiKey_Throws()
    {
        WriteUser("models:\n  m:\n    model_name: gpt-5-mini\n");
        _env.Set("OPENAI_API_KEY", null);

        Assert.Throws<ConfigValidationException>(() => Registry().Resolve("m"));
    }

    [Fact]
    public void Resolve_CustomBaseWithoutKeyEnv_SendsNoRealKey()
    {
        WriteUser("models:\n  local:\n    model_name: hosted_vllm/Qwen/Qwen2.5-0.5B-Instruct\n    api_base: http://127.0.0.1:8000/v1\n");
        _env.Set("OPENAI_API_KEY", "sk-openai");

        Registry().Resolve("local");

        Assert.Equal(("Qwen/Qwen2.5-0.5B-Instruct", "http://127.0.0.1:8000/v1", "unused"), (_built[0].Model, _built[0].ApiBase, _built[0].Key));
    }

    [Fact]
    public void Resolve_DecisionAlias_IsRejected()
    {
        WriteUser("models:\n  d:\n    client_type: decision\n    endpoint: https://gw.example.com/decide\n");

        Assert.Throws<InvalidOperationException>(() => Registry().Resolve("d"));
    }

    [Fact]
    public void Resolve_ResponsesAlias_IsNotSupported()
    {
        WriteUser("models:\n  r:\n    client_type: responses\n    api_base: http://127.0.0.1:1/v1\n");

        Assert.Throws<NotSupportedException>(() => Registry().Resolve("r"));
    }

    [Fact]
    public async Task Resolve_PassThroughSettings_ReachTheChatOptions()
    {
        WriteUser("""
            models:
              r:
                model_name: gpt-5
                api_base: http://127.0.0.1:1/v1
                temperature: 0.25
                top_p: 0.5
                max_tokens: 321
                reasoning_effort: high
            """);
        _chat.Reply("ok");

        await Registry().Resolve("r").CallAsync([LlmMessage.User("hi")], cancellationToken: TestContext.Current.CancellationToken);

        var options = _chat.Calls[0].Options!;
        Assert.Equal((0.25f, 0.5f, 321, "gpt-5"), (options.Temperature, options.TopP, options.MaxOutputTokens, options.ModelId));
        Assert.Equal(ReasoningEffort.High, options.Reasoning?.Effort);
    }

    [Fact]
    public async Task Create_ReasoningLevel_ReplacesTheDefaultEffort()
    {
        WriteUser("""
            models:
              r:
                api_base: http://127.0.0.1:1/v1
                reasoning_effort: high
                reasoning_default: low
                reasoning_levels:
                  low:
                    reasoning_effort: low
                  high:
                    reasoning_effort: high
            """);
        _chat.Reply("ok").Reply("ok");
        var registry = Registry();

        await registry.Create("r").CallAsync([LlmMessage.User("hi")], cancellationToken: TestContext.Current.CancellationToken);
        await registry.Create("r", "low").CallAsync([LlmMessage.User("hi")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ReasoningEffort.High, _chat.Calls[0].Options!.Reasoning?.Effort);
        Assert.Equal(ReasoningEffort.Low, _chat.Calls[1].Options!.Reasoning?.Effort);
    }

    [Fact]
    public void Create_UnknownReasoningLevel_Throws()
    {
        WriteUser("models:\n  r:\n    api_base: http://127.0.0.1:1/v1\n    reasoning_levels:\n      low:\n        reasoning_effort: low\n");

        var e = Assert.Throws<ConfigValidationException>(() => Registry().Create("r", "max"));

        Assert.Contains("allowed: low", e.Message);
    }

    [Fact]
    public void Create_UnsendableLevelSetting_WarnsWithKeyNamesOnly()
    {
        WriteUser("models:\n  r:\n    api_base: http://127.0.0.1:1/v1\n    extra_body:\n      trace: secret-trace-value\n");

        Registry().Create("r");

        Assert.Contains("extra_body", _logger.Text);
        Assert.DoesNotContain("secret-trace-value", _logger.Text);
    }

    [Fact]
    public void Create_InvalidReasoningEffort_Throws()
    {
        WriteUser("models:\n  r:\n    api_base: http://127.0.0.1:1/v1\n    reasoning_effort: bogus\n");

        Assert.Throws<ConfigValidationException>(() => Registry().Create("r"));
    }

    [Fact]
    public void ParseRetry_False_MakesOneAttempt()
    {
        var retry = ModelRegistry.ParseRetry("a", new Dictionary<string, object?> { ["retry_config"] = false });

        Assert.Equal((0, 0), (retry!.MaxRetries, retry.RateLimitExtraRetries));
    }

    [Fact]
    public void ParseRetry_Mapping_SetsFields()
    {
        var retry = ModelRegistry.ParseRetry("a", new Dictionary<string, object?>
        {
            ["retry_config"] = new Dictionary<string, object?> { ["max_retries"] = 1, ["base_delay"] = 0.01, ["rate_limit_extra_retries"] = 0 },
        });

        Assert.Equal((1, TimeSpan.FromSeconds(0.01), 0), (retry!.MaxRetries, retry.BaseDelay, retry.RateLimitExtraRetries));
    }

    [Fact]
    public void ParseRetry_Absent_KeepsDefaults()
    {
        Assert.Equal(new RetryOptions().MaxRetries, ModelRegistry.ParseRetry("a", new Dictionary<string, object?>())!.MaxRetries);
    }

    [Fact]
    public void ParseRetry_UnknownKeyOrBadType_Throws()
    {
        Assert.Throws<ConfigValidationException>(() => ModelRegistry.ParseRetry("a", new Dictionary<string, object?> { ["retry_config"] = "often" }));
        Assert.Throws<ConfigValidationException>(() => ModelRegistry.ParseRetry("a", new Dictionary<string, object?>
        {
            ["retry_config"] = new Dictionary<string, object?> { ["bogus"] = 1 },
        }));
    }

    [Fact]
    public void Resolve_Logs_NeverContainTheKey()
    {
        WriteUser("models:\n  keyed:\n    model_name: m\n    api_key_env: MY_TEST_KEY\n");
        _env.Set("MY_TEST_KEY", "sk-do-not-log");

        Registry().Resolve("keyed");

        Assert.DoesNotContain("sk-do-not-log", _logger.Text);
    }

    // ── Layering ──

    [Fact]
    public void Load_UserProjectEnv_LastWins()
    {
        WriteUser("models:\n  who-wins:\n    model_name: from-user\n");
        TempConfigDirs.Write(Path.Combine(_dirs.ProjectDir, LlmConfig.FileName), "models:\n  who-wins:\n    model_name: from-project\n");
        var envFile = Path.Combine(_dirs.Root, "env_config.yaml");
        TempConfigDirs.Write(envFile, "models:\n  who-wins:\n    model_name: from-env\n");
        _env.Set(LlmConfig.EnvVar, envFile);

        Assert.Equal("from-env", Registry().GetModelConfig("who-wins")!.ModelName);
    }

    [Fact]
    public void Load_NullInLaterTrustedLayer_RemovesModel()
    {
        WriteUser("models:\n  removable:\n    model_name: removable\n");
        var envFile = Path.Combine(_dirs.Root, "env.yaml");
        TempConfigDirs.Write(envFile, "models:\n  removable: null\n");
        _env.Set(LlmConfig.EnvVar, envFile);

        Assert.DoesNotContain("removable", Registry().Configs.Keys);
    }

    [Fact]
    public void Load_ProjectLayerOnly_NewAliasIsLoaded()
    {
        TempConfigDirs.Write(
            Path.Combine(_dirs.ProjectDir, LlmConfig.FileName),
            "models:\n  my-custom-model:\n    model_name: openai/my-custom-model\n    api_base: https://my-endpoint.example.com/v1\n    context_window: 128000\n");

        Assert.Equal("https://my-endpoint.example.com/v1", Registry().GetModelConfig("my-custom-model")!.ApiBase);
    }

    [Fact]
    public void Load_ModelEntryNotAMapping_IsIgnoredWithWarning()
    {
        WriteUser("models:\n  bad: 5\n  good:\n    model_name: m\n");

        var registry = Registry();

        Assert.Equal(["good"], registry.Configs.Keys);
        Assert.Contains("bad", _logger.Text);
    }

    [Fact]
    public void Load_InvalidEntry_IsIgnoredWithWarningNamingTheAlias()
    {
        WriteUser("models:\n  numeric-env:\n    model_name: m\n    api_key_env: 12345\n");

        var registry = Registry();

        Assert.Empty(registry.Configs);
        Assert.Contains("numeric-env", _logger.Text);
        Assert.Contains("api_key_env", _logger.Text);
    }

    [Fact]
    public void Load_ModelsNotAMapping_IsIgnoredWithWarning()
    {
        WriteUser("models: [a, b]\n");

        Assert.Empty(Registry().Configs);
        Assert.Contains("'models' is not a mapping", _logger.Text);
    }

    [Fact]
    public void Load_ReloadPicksUpChanges()
    {
        Assert.Empty(Registry().Configs);
        WriteUser("models:\n  one:\n    model_name: one\n");

        Assert.Contains("one", Registry().Configs.Keys);
    }
}
