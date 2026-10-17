// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py (no upstream test file)

using Nooa.Errors;
using Nooa.Tests.Llm;
using Nooa.Util;

namespace Nooa.Tests.Util;

public class QuickstartTests
{
    private static (string Model, string? Base, string Key, string Name) Pick(Dictionary<string, string> env)
    {
        (string Model, string? Base, string Key)? built = null;
        var (name, _) = Quickstart.CreateDefaultLlm(
            n => env.GetValueOrDefault(n),
            (m, b, k) =>
            {
                built = (m, b, k);
                return new ScriptedChatClient();
            });
        return (built!.Value.Model, built.Value.Base, built.Value.Key, name);
    }

    [Fact]
    public void CreateDefaultLlm_NvidiaKey_UsesNim()
    {
        var picked = Pick(new() { ["NVIDIA_API_KEY"] = "nk", ["OPENAI_API_KEY"] = "ok" });

        Assert.Equal(("nvidia/nemotron-3-super-120b-a12b", "https://integrate.api.nvidia.com/v1", "nk"), (picked.Model, picked.Base, picked.Key));
        Assert.StartsWith("nvidia_nim/", picked.Name);
    }

    [Fact]
    public void CreateDefaultLlm_OpenAiKey_UsesDefaultModel()
    {
        var picked = Pick(new() { ["OPENAI_API_KEY"] = "ok" });

        Assert.Equal(("gpt-5-mini", (string?)null, "ok"), (picked.Model, picked.Base, picked.Key));
        Assert.Equal(Quickstart.DefaultModel, picked.Name);
    }

    [Theory]
    [InlineData("NVIDIA_INFERENCE_API_KEY")]
    [InlineData("NVIDIA_INTERNAL_API_KEY")]
    public void CreateDefaultLlm_GatewayKey_UsesGatewayBase(string variable)
    {
        var picked = Pick(new() { [variable] = "gk" });

        Assert.Equal(("azure/openai/gpt-5-mini", "https://inference-api.nvidia.com/v1", "gk"), (picked.Model, picked.Base, picked.Key));
    }

    [Theory]
    [InlineData("http://127.0.0.1:11435/v1")]
    [InlineData("http://localhost:8080/v1")]
    [InlineData("https://example.com/v1")]
    public void CreateDefaultLlm_OpenAiBaseUrl_IsUsed(string baseUrl)
    {
        var picked = Pick(new() { ["OPENAI_API_KEY"] = "ok", ["OPENAI_BASE_URL"] = baseUrl });

        Assert.Equal(("gpt-5-mini", baseUrl, "ok"), (picked.Model, picked.Base, picked.Key));
    }

    [Theory]
    [InlineData("http://example.com/v1")]
    [InlineData("https://user:secret@example.com/v1")]
    [InlineData("ftp://127.0.0.1/v1")]
    [InlineData("not a url")]
    public void CreateDefaultLlm_UnsafeOpenAiBaseUrl_ThrowsWithoutTheValue(string baseUrl)
    {
        var e = Assert.Throws<ConfigValidationException>(
            () => Pick(new() { ["OPENAI_API_KEY"] = "ok", ["OPENAI_BASE_URL"] = baseUrl }));

        Assert.DoesNotContain(baseUrl, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDefaultLlm_NooaModel_ReplacesTheRouteModel()
    {
        var picked = Pick(new() { ["OPENAI_API_KEY"] = "ok", ["NOOA_MODEL"] = "sonnet" });

        Assert.Equal(("sonnet", (string?)null, "ok"), (picked.Model, picked.Base, picked.Key));
        Assert.Equal("sonnet", picked.Name);
    }

    [Fact]
    public void CreateDefaultLlm_OpenAiBaseUrlWithoutOpenAiKey_IsIgnored()
    {
        var picked = Pick(new() { ["NVIDIA_API_KEY"] = "nk", ["OPENAI_BASE_URL"] = "http://example.com/v1" });

        Assert.Equal("https://integrate.api.nvidia.com/v1", picked.Base);
    }

    [Fact]
    public void CreateDefaultLlm_EmptyKey_CountsAsUnset()
    {
        Assert.Throws<ConfigValidationException>(() => Pick(new() { ["OPENAI_API_KEY"] = "" }));
    }

    [Fact]
    public void CreateDefaultLlm_NoCredential_ThrowsWithoutNamingAValue()
    {
        var e = Assert.Throws<ConfigValidationException>(() => Pick([]));

        Assert.Contains("OPENAI_API_KEY", e.Message);
    }
}
