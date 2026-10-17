// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_secret_scrubber.py

using System.Text.Json.Nodes;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(EnvironmentCollection.Name)]
public class SecretScrubberTests
{
    private const string _redacted = SecretScrubber.Redacted;

    [Theory]
    [InlineData("key is AKIAIOSFODNN7EXAMPLE", "AKIAIOSFODNN7EXAMPLE")]
    [InlineData("aws_secret_access_key = wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "wJalrXUtnFEMI")]
    [InlineData("token: ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmn", "ghp_")]
    [InlineData("GITLAB_TOKEN=glpat-abcdef1234567890abcd", "glpat-")]
    [InlineData("slack: xoxb-1234567890-abcdefghij", "xoxb-")]
    [InlineData("STRIPE_KEY=sk_live_abcdefghijklmnopqrstuv", "sk_live_")]
    [InlineData("export NVIDIA_KEY=nvapi-abcdefghijklmnopqrstuv", "nvapi-")]
    [InlineData("OPENAI_API_KEY=sk-abcdefghijklmnopqrstuv", "sk-abcdefghijklmnopqrstuv")]
    [InlineData("key=sk-ant-abcdefghijklmnopqrstuvwxyz", "sk-ant-")]
    [InlineData("GOOGLE_KEY=AIzaSyA1234567890abcdefghijklmnopqrstuv", "AIzaSy")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abcdef", "eyJhbGci")]
    [InlineData("api_key=abcdefghijklmnopqrstuvwxyz1234", "abcdefghijklmnopqrstuvwxyz1234")]
    [InlineData("client_secret=s3cr3t", "s3cr3t")]
    [InlineData("{\"api_key\": \"abcdefghijklmnopqrstuvwxyz1234\"}", "abcdefghijklmnopqrstuvwxyz1234")]
    public void ScrubString_KnownSecretShapes_AreRedacted(string text, string secret)
    {
        var (result, count) = SecretScrubber.ScrubString(text);

        Assert.DoesNotContain(secret, result, StringComparison.Ordinal);
        Assert.Contains(_redacted, result, StringComparison.Ordinal);
        Assert.True(count >= 1);
    }

    [Fact]
    public void ScrubString_HexToken_IsRedacted()
    {
        var (result, _) = SecretScrubber.ScrubString("token=" + new string('a', 64));

        Assert.DoesNotContain(new string('a', 64), result, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrubString_PrivateKeyBlock_IsRedactedWhole()
    {
        var (result, _) = SecretScrubber.ScrubString(
            "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEAsecretkeymaterial1234567890\n-----END RSA PRIVATE KEY-----");

        Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", result, StringComparison.Ordinal);
        Assert.DoesNotContain("MIIEowIBAAK", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("This is just normal code output with no secrets at all.")]
    [InlineData("")]
    [InlineData("hello")]
    public void ScrubString_CleanText_IsUnchanged(string text) =>
        Assert.Equal((text, 0), SecretScrubber.ScrubString(text));

    [Fact]
    public void ScrubString_MultipleSecrets_AreEachRedactedAndSurroundingTextStays()
    {
        var (result, count) = SecretScrubber.ScrubString("before AKIAIOSFODNN7EXAMPLE and ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmn after");

        Assert.StartsWith("before ", result, StringComparison.Ordinal);
        Assert.EndsWith(" after", result, StringComparison.Ordinal);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ScrubString_AdversarialInput_FinishesQuickly()
    {
        var hostile = string.Concat(Enumerable.Repeat("api_key=" + new string('a', 200) + " ", 5000));

        var started = DateTime.UtcNow;
        SecretScrubber.ScrubString(hostile);

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ScrubString_ValueOfACredentialEnvironmentVariable_IsRedacted()
    {
        using var env = new EnvVars();
        env.Set("MY_SERVICE_API_KEY", "plain-value-without-any-known-shape");
        env.Set("MY_SHORT_TOKEN", "abc");

        var (result, count) = SecretScrubber.ScrubString("the key is plain-value-without-any-known-shape, the token is abc");

        Assert.Equal("the key is [REDACTED], the token is abc", result);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubString_ValueOfANonCredentialEnvironmentVariable_IsKept()
    {
        using var env = new EnvVars();
        env.Set("MY_SERVICE_NAME", "visible-service-name");

        Assert.Equal(("visible-service-name", 0), SecretScrubber.ScrubString("visible-service-name"));
    }

    [Fact]
    public void ScrubValue_JsonWithCredentialKey_RedactsTheValueAndKeepsUnicode()
    {
        var (result, count) = SecretScrubber.ScrubValue(" \n{\"api_key\":\"synthetic-secret\",\"text\":\"héllo 世界\"}");

        var json = JsonNode.Parse((string)result!)!;
        Assert.Equal(_redacted, (string)json["api_key"]!);
        Assert.Equal("héllo 世界", (string)json["text"]!);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubValue_JsonArrayOfObjects_IsScrubbed()
    {
        var (result, count) = SecretScrubber.ScrubValue("[{\"password\":\"p\"}]");

        Assert.Equal("[{\"password\":\"[REDACTED]\"}]", result);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("ordinary code output")]
    [InlineData(" \n héllo 世界")]
    [InlineData("")]
    [InlineData("42")]
    [InlineData(" \n{ \"text\": \"héllo 世界\" }\n")]
    public void ScrubValue_CleanText_IsReturnedUnchanged(string text) =>
        Assert.Equal((text, 0), SecretScrubber.ScrubValue(text));

    [Fact]
    public void ScrubValue_InvalidJson_FallsBackToTextScrubbing() =>
        Assert.Equal(("[progress] api_key=[REDACTED]", 1), SecretScrubber.ScrubValue("[progress] api_key=synthetic-secret"));

    [Fact]
    public void ScrubValue_ExcessiveNesting_RedactsTheEntireValue()
    {
        var value = new string('[', 2000) + "{\"encrypted_content\":\"private\"}" + new string(']', 2000);

        Assert.Equal((_redacted, 1), SecretScrubber.ScrubValue(value));
    }

    [Fact]
    public void ScrubValue_BracketsInsideStrings_DoNotCountAsNesting()
    {
        var value = "{\"text\":\"" + new string('[', 10) + "\"}";

        Assert.Equal((value, 0), SecretScrubber.ScrubValue(value));
    }

    [Fact]
    public void ScrubValue_NestedSensitiveKeys_AreRedacted()
    {
        var (result, count) = SecretScrubber.ScrubValue("{\"safe\":{\"client_secret\":\"short\",\"refresh_token\":\"provider-specific\"}}");

        Assert.Equal("{\"safe\":{\"client_secret\":\"[REDACTED]\",\"refresh_token\":\"[REDACTED]\"}}", result);
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData("{\"input\":[{\"type\":\"reasoning\",\"encrypted_content\":\"opaque-openai-state\"}]}", "opaque-openai-state")]
    [InlineData("{\"thinking_blocks\":[{\"type\":\"thinking\",\"signature\":\"anthropic-sig\"}]}", "anthropic-sig")]
    [InlineData("{\"thinking_blocks\":[{\"type\":\"redacted_thinking\",\"data\":\"opaque-data\"}]}", "opaque-data")]
    [InlineData("{\"provider_specific_fields\":{\"thought_signature\":\"gemini-sig\"}}", "gemini-sig")]
    [InlineData("{\"provider_specific_fields\":{\"thought_signatures\":[\"gemini-sig\"]}}", "gemini-sig")]
    [InlineData("{\"providerSpecificFields\":{\"thoughtSignature\":\"gemini-camel-sig\"}}", "gemini-camel-sig")]
    [InlineData("{\"providerSpecificFields\":{\"thoughtSignatures\":[\"gemini-camel-sig\"]}}", "gemini-camel-sig")]
    public void ScrubValue_OpaqueProviderState_IsRedacted(string json, string secret)
    {
        var (result, count) = SecretScrubber.ScrubValue(json);

        Assert.DoesNotContain(secret, (string)result!, StringComparison.Ordinal);
        Assert.Contains(_redacted, (string)result!, StringComparison.Ordinal);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubValue_GeminiInlineToolCallSignature_IsRedactedFromFlatAndJsonText()
    {
        Assert.Equal(("call_1__thought__[REDACTED]", 1), SecretScrubber.ScrubValue("call_1__thought__Z2VtaW5pLXNpZw=="));

        var (json, count) = SecretScrubber.ScrubValue("{\"tool_calls\":[{\"id\":\"call_1__thought__Z2VtaW5pLXNpZw==\"}]}");

        Assert.DoesNotContain("Z2VtaW5pLXNpZw==", (string)json!, StringComparison.Ordinal);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubValue_StringArray_IsScrubbedElementWise()
    {
        var (result, count) = SecretScrubber.ScrubValue(new[] { "AKIAIOSFODNN7EXAMPLE", "normal" });

        Assert.Equal(["[REDACTED]", "normal"], Assert.IsType<string[]>(result));
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubValue_JsonNode_IsScrubbedByKey()
    {
        var (result, count) = SecretScrubber.ScrubValue(new JsonObject { ["authorization"] = "x" });

        Assert.Equal("[REDACTED]", (string)((JsonObject)result!)["authorization"]!);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(true)]
    [InlineData(null)]
    [InlineData(1.5)]
    public void ScrubValue_Scalars_PassThrough(object? value) =>
        Assert.Equal((value, 0), SecretScrubber.ScrubValue(value));

    [Theory]
    [InlineData("Authorization", "sensitive_key")]
    [InlineData("x-api-key", "sensitive_key")]
    [InlineData("http.request.header.authorization", "sensitive_key")]
    [InlineData("encryptedContent", null)]
    [InlineData("thoughtSignature", "opaque_provider_state")]
    [InlineData("agent.method_signature", null)]
    [InlineData("llm.token_count.total", null)]
    [InlineData("apiKey", "sensitive_key")]
    [InlineData("accessToken", "sensitive_key")]
    [InlineData("clientSecret", "sensitive_key")]
    [InlineData("OPENAI_APIKEY", "sensitive_key")]
    [InlineData("userPassword", "sensitive_key")]
    public void RedactKey_ClassifiesKeys(string key, string? reason) =>
        Assert.Equal(reason, SecretScrubber.RedactKey(key));

    [Theory]
    [InlineData("token", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("api_token", "short-value")]
    [InlineData("aws_secret_access_key", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY")]
    public void ScrubValue_JsonValueThatNeedsItsKey_IsRedacted(string key, string secret)
    {
        var (result, count) = SecretScrubber.ScrubValue("{\"" + key + "\":\"" + secret + "\",\"note\":\"ok\"}");

        Assert.DoesNotContain(secret, (string)result!, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"ok\"", (string)result!, StringComparison.Ordinal);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubValue_JsonObjectKeyThatIsASecret_IsRedacted()
    {
        var (result, count) = SecretScrubber.ScrubValue("{\"AKIAIOSFODNN7EXAMPLE\":\"v\"}");

        Assert.Equal("{\"[REDACTED]\":\"v\"}", result);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ScrubString_EnvironmentValueThatARegexWouldSplit_IsRedactedWhole()
    {
        using var env = new EnvVars();
        env.Set("QS_SPLIT_API_KEY", "prefix-sk-abcdefghijklmnopqrstuv-suffix123");

        var (result, _) = SecretScrubber.ScrubString("value: prefix-sk-abcdefghijklmnopqrstuv-suffix123 end");

        Assert.Equal("value: [REDACTED] end", result);
    }

    [Theory]
    [InlineData("sk-proj-abcdefghijklmnopqrstuvwxyz")]
    [InlineData("sk-svcacct-abcdefghijklmnopqrstuvwxyz")]
    [InlineData("sk-admin-abcdefghijklmnopqrstuvwxyz")]
    [InlineData("sk-abc_defghijklmnopqrstuv-wxyz")]
    public void ScrubString_OpenAiKeyVariants_AreRedactedWhole(string key)
    {
        var (result, _) = SecretScrubber.ScrubString("use " + key + " now");

        Assert.Equal("use [REDACTED] now", result);
    }

    [Fact]
    public void ScrubString_AnthropicKey_StillMatchesFirst() =>
        Assert.Equal("k [REDACTED]", SecretScrubber.ScrubString("k sk-ant-api03-abcdefghijklmnopqrstuv").Text);
}
