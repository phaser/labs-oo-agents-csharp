// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/config/test_resolved_config.py

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class ModelConfigTests
{
    [Fact]
    public void FromRegistry_NoModelName_DefaultsToAlias()
    {
        var config = ModelConfig.FromRegistry("my-alias", new Dictionary<string, object?>
        {
            ["api_base"] = "https://example.test/v1",
            ["api_key_env"] = "K",
        });

        Assert.Equal("my-alias", config.ModelName);
        Assert.Equal("https://example.test/v1", config.ApiBase);
        Assert.Equal("K", config.ApiKeyEnv);
    }

    [Fact]
    public void FromRegistry_UnknownKey_StaysInExtra()
    {
        var config = ModelConfig.FromRegistry("a", new Dictionary<string, object?> { ["model_name"] = "m", ["num_retries"] = 7 });

        Assert.Equal(7, config.Extra["num_retries"]);
    }

    [Fact]
    public void FromRegistry_TypedValues_AreRead()
    {
        var config = ModelConfig.FromRegistry("a", new Dictionary<string, object?>
        {
            ["context_window"] = 128000,
            ["max_tokens"] = 4096L,
            ["temperature"] = 0,
            ["top_p"] = 0.5,
        });

        Assert.Equal((128000, 4096, 0.0, 0.5), (config.ContextWindow, config.MaxTokens, config.Temperature, config.TopP));
    }

    [Theory]
    [InlineData("context_window", "many")]
    [InlineData("temperature", "hot")]
    [InlineData("api_base", 5)]
    [InlineData("api_key_env", 12345)]
    [InlineData("reasoning_levels", "low")]
    public void FromRegistry_WrongType_Throws(string key, object value)
    {
        var e = Assert.Throws<ConfigValidationException>(() =>
            ModelConfig.FromRegistry("a", new Dictionary<string, object?> { [key] = value }));

        Assert.Contains(key, e.Message);
    }

    [Fact]
    public void FromRegistry_ReasoningLevelNotAMapping_Throws()
    {
        var raw = new Dictionary<string, object?> { ["reasoning_levels"] = new Dictionary<string, object?> { ["low"] = "low" } };

        Assert.Throws<ConfigValidationException>(() => ModelConfig.FromRegistry("a", raw));
    }

    [Theory]
    [InlineData("1BAD")]
    [InlineData("has space")]
    [InlineData("A;B")]
    public void FromRegistry_BadEnvName_Throws(string name)
    {
        Assert.Throws<ConfigValidationException>(() =>
            ModelConfig.FromRegistry("a", new Dictionary<string, object?> { ["api_key_env"] = name }));
    }

    [Theory]
    [InlineData("ftp://host/v1")]
    [InlineData("not a url")]
    [InlineData("file:///etc/passwd")]
    public void FromRegistry_BadApiBase_Throws(string apiBase)
    {
        Assert.Throws<ConfigValidationException>(() =>
            ModelConfig.FromRegistry("a", new Dictionary<string, object?> { ["api_base"] = apiBase }));
    }

    [Theory]
    [InlineData("https://user:pass@host.example.com/v1")]
    [InlineData("https://host.example.com/v1?api_key=sk-secret")]
    [InlineData("https://host.example.com/v1#frag")]
    public void FromRegistry_UrlWithCredentialParts_ThrowsWithoutEchoingTheUrl(string url)
    {
        foreach (var field in new[] { "api_base", "endpoint" })
        {
            var e = Assert.Throws<ConfigValidationException>(() =>
                ModelConfig.FromRegistry("a", new Dictionary<string, object?> { [field] = url }));

            Assert.Contains(field, e.Message);
            Assert.DoesNotContain("pass", e.Message.Replace("must not contain", ""));
            Assert.DoesNotContain("sk-secret", e.Message);
        }
    }

    [Fact]
    public void ToString_RedactsUserInfoAndQueryAddedWithWith()
    {
        var config = ModelConfig.FromRegistry("a", new Dictionary<string, object?>()) with { ApiBase = "https://u:pw-secret@h.example.com/v1?key=sk-secret" };

        var text = config.ToString();

        Assert.DoesNotContain("pw-secret", text);
        Assert.DoesNotContain("sk-secret", text);
        Assert.Contains("h.example.com/v1", text);
    }

    [Fact]
    public void ToString_NeverPrintsExtraValues()
    {
        var config = ModelConfig.FromRegistry("a", new Dictionary<string, object?>
        {
            ["api_key"] = "sk-literal-secret",
            ["api_key_env"] = "MY_KEY",
        });

        var text = config.ToString();

        Assert.DoesNotContain("sk-literal-secret", text);
        Assert.Contains("api_key", text);
        Assert.Contains("MY_KEY", text);
    }

    [Fact]
    public void With_ChangesACopyOnly()
    {
        var config = ModelConfig.FromRegistry("a", new Dictionary<string, object?> { ["model_name"] = "m" });

        var changed = config with { ModelName = "other" };

        Assert.Equal("m", config.ModelName);
        Assert.Equal("other", changed.ModelName);
    }
}
