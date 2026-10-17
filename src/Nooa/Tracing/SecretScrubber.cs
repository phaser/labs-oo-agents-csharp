// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_secret_scrubber.py

using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Nooa.Tracing;

/// <summary>
/// Redacts secrets (API keys, tokens, private keys, credential-shaped environment values) from span attribute values.
/// </summary>
/// <remarks>
/// <para>
/// The patterns are high precision, not entropy based. Every regex is linear (<see cref="RegexOptions.NonBacktracking"/>)
/// and has a timeout. A timeout fails closed: the whole string is redacted. A value under a credential key is always replaced.
/// </para>
/// <para>
/// Deviation from upstream: the value of every environment variable with a credential-shaped name
/// (<see cref="SecretEnvDenylist.IsCredentialShaped(string)"/>, 8 characters or more) is redacted too, wherever it appears.
/// </para>
/// </remarks>
public static class SecretScrubber
{
    /// <summary>The text that replaces a secret.</summary>
    public const string Redacted = "[REDACTED]";

    private const int _minEnvSecretLength = 8;
    private const int _maxJsonDepth = 64;

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> _sensitiveKeys =
    [
        "authorization", "proxy_authorization", "api_key", "x_api_key", "access_token", "refresh_token", "auth_token",
        "session_token", "id_token", "client_secret", "client_assertion", "password", "passwd", "private_key",
        "secret_key", "code_verifier", "cookie", "set_cookie",
    ];

    // Provider-owned replay state is not a user credential, but it has the same telemetry rule:
    // it may go back to its matching provider and nowhere else.
    private static readonly HashSet<string> _opaqueProviderStateKeys =
    [
        "encrypted_content", "inline_thought_signature", "signature", "native", "nooa_llm_state", "thought_signature",
        "thought_signatures", "thoughtsignature", "thoughtsignatures",
    ];

    private static readonly (string Name, Regex Pattern)[] _patterns =
    [
        ("gemini_inline_thought_signature", Make(@"__thought__(?<secret>[A-Za-z0-9+/=_-]+)")),
        ("aws_access_key", Make(@"(?<secret>(?:AKIA|ASIA|AIDA|AROA)[A-Z0-9]{16})")),
        ("aws_secret_key", Make(@"(?:aws_secret_access_key|secret.?key)[""']?\s*[=:]\s*[""'\s]*(?<secret>[A-Za-z0-9/+=]{40})", RegexOptions.IgnoreCase)),
        ("github_token", Make(@"(?<secret>gh[pousr]_[A-Za-z0-9_]{36,})")),
        ("gitlab_token", Make(@"(?<secret>glpat-[A-Za-z0-9\-_]{20,})")),
        ("slack_token", Make(@"(?<secret>xox[bporas]-[A-Za-z0-9\-]{10,})")),
        ("stripe_key", Make(@"(?<secret>(?:sk|pk|rk)_(?:live|test)_[A-Za-z0-9]{20,})")),
        ("nvidia_api_key", Make(@"(?<secret>nvapi-[A-Za-z0-9_\-]{20,})")),
        ("anthropic_key", Make(@"(?<secret>sk-ant-[A-Za-z0-9_\-]{20,})")),
        ("openai_key", Make(@"(?<secret>sk-(?:proj-|svcacct-|admin-)?[A-Za-z0-9_\-]{20,})")),
        ("google_key", Make(@"(?<secret>AIza[A-Za-z0-9_\-]{35})")),
        ("bearer_token", Make(@"[Bb]earer\s+(?<secret>[A-Za-z0-9_.\-/+=]{20,})")),
        ("generic_api_key", Make(
            @"(?:api[_-]?key|api[_-]?token|auth[_-]?token|access[_-]?token|client[_-]?secret|refresh[_-]?token|code[_-]?verifier|secret[_-]?key|private[_-]?key|password|passwd)[""']?\s*[=:]+\s*[""'\s]*(?<secret>[A-Za-z0-9_.\-/+=]{1,})",
            RegexOptions.IgnoreCase)),
        ("private_key", Make(@"(?<secret>-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----)")),
        ("hex_token", Make(@"(?:token|key|secret|hash)[""']?\s*[=:]\s*[""'\s]*(?<secret>[0-9a-f]{64,})", RegexOptions.IgnoreCase)),
    ];

    /// <summary>Returns the reason to redact a value under this attribute or JSON key, or null.</summary>
    public static string? RedactKey(string key)
    {
        var normalized = Normalize(key);
        if (_sensitiveKeys.Contains(normalized) || _sensitiveKeys.Any(s => normalized.EndsWith("_" + s, StringComparison.Ordinal)))
        {
            return "sensitive_key";
        }

        return _opaqueProviderStateKeys.Contains(normalized) ? "opaque_provider_state" : null;
    }

    /// <summary>Redacts the secrets in a string. Returns the new text and the number of redactions.</summary>
    public static (string Text, int Count) ScrubString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (text, 0);
        }

        var count = 0;
        var result = text;

        // The environment pass runs first: a regex replacement could split an environment value and hide it from this pass.
        foreach (var secret in EnvironmentSecrets())
        {
            if (result.Contains(secret, StringComparison.Ordinal))
            {
                result = result.Replace(secret, Redacted, StringComparison.Ordinal);
                count++;
            }
        }

        try
        {
            foreach (var (_, pattern) in _patterns)
            {
                result = pattern.Replace(result, m =>
                {
                    count++;
                    var secret = m.Groups["secret"];
                    return m.Value.Replace(secret.Value, Redacted, StringComparison.Ordinal);
                });
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return (Redacted, count + 1);
        }

        return (result, count);
    }

    /// <summary>
    /// Redacts the secrets in an attribute value. A string that holds a JSON object or array is parsed, so a value under
    /// a credential key is replaced even when it is short. Other values pass through.
    /// </summary>
    public static (object? Value, int Count) ScrubValue(object? value)
    {
        switch (value)
        {
            case string text:
                return ScrubText(text);
            case JsonNode node:
                var (scrubbed, count) = ScrubNode(node);
                return (scrubbed, count);
            case Array array when array is not byte[]:
                var items = new List<object?>();
                var total = 0;
                foreach (var item in array)
                {
                    var (scrubbedItem, n) = ScrubValue(item);
                    items.Add(scrubbedItem);
                    total += n;
                }

                return total == 0 ? (value, 0) : (ToTyped(array.GetType().GetElementType()!, items), total);
            default:
                return (value, 0);
        }
    }

    private static (object? Value, int Count) ScrubText(string text)
    {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        if (start >= text.Length || (text[start] != '{' && text[start] != '['))
        {
            var (plain, plainCount) = ScrubString(text);
            return (plain, plainCount);
        }

        if (ExceedsDepth(text))
        {
            // A structure this deep is hostile or broken. Redact all of it: a partial scrub could leak a key that sits inside.
            return (Redacted, 1);
        }

        JsonNode? decoded;
        try
        {
            decoded = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = _maxJsonDepth });
        }
        catch (JsonException)
        {
            var (plain, plainCount) = ScrubString(text);
            return (plain, plainCount);
        }

        var (node, count) = ScrubNode(decoded);
        return count == 0 ? (text, 0) : (node?.ToJsonString() ?? "null", count);
    }

    private static bool ExceedsDepth(string text)
    {
        var depth = 0;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }
            }
            else if (c == '"')
            {
                inString = true;
            }
            else if ((c == '{' || c == '[') && ++depth > _maxJsonDepth)
            {
                return true;
            }
            else if ((c == '}' || c == ']') && depth > 0)
            {
                depth--;
            }
        }

        return false;
    }

    private static (JsonNode? Node, int Count) ScrubNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var count = 0;
                var isThinking = obj["type"]?.GetValueKind() == JsonValueKind.String && obj["type"]!.GetValue<string>() == "thinking";
                var isRedactedThinking = obj["type"]?.GetValueKind() == JsonValueKind.String && obj["type"]!.GetValue<string>() == "redacted_thinking";
                var result = new JsonObject();
                foreach (var (key, item) in obj)
                {
                    var reason = RedactKey(key);
                    if (reason is null && ((isThinking && key == "signature") || (isRedactedThinking && key == "data")))
                    {
                        reason = "opaque_provider_state";
                    }

                    if (reason is not null)
                    {
                        result[key] = Redacted;
                        count++;
                    }
                    else
                    {
                        // A key can carry a secret too, for example a map from token to value.
                        var (cleanKey, keyCount) = ScrubString(key);
                        var (scrubbed, n) = ScrubNode(item);
                        result[cleanKey] = scrubbed;
                        count += n + keyCount;
                    }
                }

                return (result, count);
            case JsonArray array:
                var items = new JsonArray();
                var total = 0;
                foreach (var item in array)
                {
                    var (scrubbed, n) = ScrubNode(item);
                    items.Add(scrubbed);
                    total += n;
                }

                return (items, total);
            case JsonValue v when v.TryGetValue<string>(out var s):
                var (text, c) = ScrubText(s);
                return (JsonValue.Create((string)text!), c);
            default:
                return (node?.DeepClone(), 0);
        }
    }

    private static object ToTyped(Type element, List<object?> items)
    {
        var array = Array.CreateInstance(element, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }

    private static string Normalize(string key) =>
        Regex.Replace(key.ToLowerInvariant(), "[^a-z0-9]+", "_", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, _timeout).Trim('_');

    private static List<string> EnvironmentSecrets()
    {
        var secrets = new List<string>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name
                && entry.Value is string value
                && value.Length >= _minEnvSecretLength
                && SecretEnvDenylist.IsCredentialShaped(name))
            {
                secrets.Add(value);
            }
        }

        // Longest first, so a value that contains another is replaced whole.
        secrets.Sort((a, b) => b.Length.CompareTo(a.Length));
        return secrets;
    }

    private static Regex Make(string pattern, RegexOptions extra = RegexOptions.None) =>
        new(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | extra, _timeout);
}
