// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/model_config.py

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Nooa.Errors;

namespace Nooa.Config;

/// <summary>
/// One model alias from <c>llm_config.yaml</c> (for example <c>claude-opus-4-8</c>), as typed values.
/// </summary>
/// <remarks>
/// <para>
/// Common fields are typed. Any other key stays in <see cref="Extra"/> (passthrough settings such as
/// <c>retry_config</c> or <c>extra_body</c>). The record is immutable. It does not use the explicit-field tracking of
/// <see cref="ConfigBase"/>, because a layer replaces a whole alias and no field-wise merge takes place.
/// </para>
/// <para>
/// <see cref="ApiKeyEnv"/> is the name of an environment variable, never a key. A literal key in the YAML file is not
/// read. <see cref="ToString"/> prints the names of the <see cref="Extra"/> keys and never their values.
/// </para>
/// </remarks>
public sealed partial record ModelConfig
{
    /// <summary>The model string for the provider. The alias name is used when the entry has none.</summary>
    public string? ModelName { get; init; }

    /// <summary>The base URL of an OpenAI-compatible endpoint.</summary>
    public string? ApiBase { get; init; }

    /// <summary>The endpoint of a decision model. Decision models are not supported.</summary>
    public string? Endpoint { get; init; }

    /// <summary>The name of the environment variable that holds the API key.</summary>
    public string? ApiKeyEnv { get; init; }

    /// <summary>The client kind: <c>completion</c> (the default), <c>responses</c> or <c>decision</c>.</summary>
    public string? ClientType { get; init; }

    /// <summary>The wire style of a decision model.</summary>
    public string? ApiStyle { get; init; }

    /// <summary>The context window in tokens.</summary>
    public int? ContextWindow { get; init; }

    /// <summary>The reply token cap.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>The sampling temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>The nucleus sampling probability.</summary>
    public double? TopP { get; init; }

    /// <summary>The declared reasoning levels: level name to request settings.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? ReasoningLevels { get; init; }

    /// <summary>The default level of the route, as documentation.</summary>
    public string? ReasoningDefault { get; init; }

    /// <summary>The level that the client selects.</summary>
    public string? ReasoningLevel { get; init; }

    /// <summary>The settings that have no typed property, by YAML key.</summary>
    public IReadOnlyDictionary<string, object?> Extra { get; init; } = ReadOnlyDictionary<string, object?>.Empty;

    /// <summary>
    /// Builds the typed config from the raw mapping of an alias. <see cref="ModelName"/> defaults to <paramref name="name"/>.
    /// </summary>
    /// <exception cref="ConfigValidationException">A value has the wrong type, <c>api_base</c> is not an http or https URL, or <c>api_key_env</c> is not an environment variable name.</exception>
    public static ModelConfig FromRegistry(string name, IReadOnlyDictionary<string, object?> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var typed = new HashSet<string>(StringComparer.Ordinal);

        T? Get<T>(string key, Func<object, T?> convert, string expected)
            where T : struct
        {
            typed.Add(key);
            if (raw.GetValueOrDefault(key) is not { } value)
            {
                return null;
            }

            return convert(value) ?? throw Invalid(name, key, expected, value);
        }

        string? GetString(string key)
        {
            typed.Add(key);
            return raw.GetValueOrDefault(key) switch
            {
                null => null,
                string s => s,
                var other => throw Invalid(name, key, "a string", other),
            };
        }

        var config = new ModelConfig
        {
            ModelName = GetString("model_name") ?? name,
            ApiBase = GetString("api_base"),
            Endpoint = GetString("endpoint"),
            ApiKeyEnv = GetString("api_key_env"),
            ClientType = GetString("client_type"),
            ApiStyle = GetString("api_style"),
            ContextWindow = Get("context_window", ToInt, "an integer"),
            MaxTokens = Get("max_tokens", ToInt, "an integer"),
            Temperature = Get("temperature", ToDouble, "a number"),
            TopP = Get("top_p", ToDouble, "a number"),
            ReasoningLevels = ParseLevels(name, raw.GetValueOrDefault("reasoning_levels")),
            ReasoningDefault = GetString("reasoning_default"),
            ReasoningLevel = GetString("reasoning_level"),
        };
        typed.Add("reasoning_levels");
        config = config with
        {
            Extra = new ReadOnlyDictionary<string, object?>(
                raw.Where(kv => !typed.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal)),
        };

        if (config.ApiKeyEnv is { } env && !EnvNameRegex().IsMatch(env))
        {
            throw new ConfigValidationException($"Model '{name}' has an invalid api_key_env: expected a shell-style environment variable name");
        }

        if (config.ApiBase is { } apiBase
            && !(Uri.TryCreate(apiBase, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
        {
            throw new ConfigValidationException($"Model '{name}' has an invalid api_base: expected an http or https URL");
        }

        return config;
    }

    /// <summary>Prints the typed fields and the names of the extra keys. No value of <see cref="Extra"/> appears.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"ModelName = {ModelName}, ApiBase = {ApiBase}, Endpoint = {Endpoint}, ApiKeyEnv = {ApiKeyEnv}, ")
            .Append(CultureInfo.InvariantCulture, $"ClientType = {ClientType}, ApiStyle = {ApiStyle}, ContextWindow = {ContextWindow}, MaxTokens = {MaxTokens}, ")
            .Append(CultureInfo.InvariantCulture, $"Temperature = {Temperature}, TopP = {TopP}, ReasoningDefault = {ReasoningDefault}, ReasoningLevel = {ReasoningLevel}, ")
            .Append(CultureInfo.InvariantCulture, $"ReasoningLevels = [{string.Join(", ", ReasoningLevels?.Keys ?? [])}], ExtraKeys = [{string.Join(", ", Extra.Keys)}]");
        return true;
    }

    private static ConfigValidationException Invalid(string name, string key, string expected, object value) =>
        new($"Model '{name}' has an invalid {key}: expected {expected}, got {value.GetType().Name}");

    // YAML integers arrive as the smallest fitting integral type (Int16, Int32, Int64 ...), so convert by value.
    private static bool IsIntegral(object value) => value is sbyte or byte or short or ushort or int or uint or long;

    private static int? ToInt(object value) =>
        IsIntegral(value) && Convert.ToInt64(value, CultureInfo.InvariantCulture) is >= int.MinValue and <= int.MaxValue and var n ? (int)n : null;

    /// <summary>Converts a YAML number (integral or floating-point) to a double. Returns null for any other value.</summary>
    internal static double? ToDouble(object value) => value switch
    {
        _ when IsIntegral(value) => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        double d => d,
        float f => f,
        decimal m => (double)m,
        _ => null,
    };

    private static Dictionary<string, IReadOnlyDictionary<string, object?>>? ParseLevels(string name, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not IReadOnlyDictionary<string, object?> levels)
        {
            throw Invalid(name, "reasoning_levels", "a mapping", value);
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var (level, settings) in levels)
        {
            result[level] = settings as IReadOnlyDictionary<string, object?>
                ?? throw new ConfigValidationException($"Model '{name}' reasoning level '{level}' must be a mapping of request settings");
        }

        return result;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvNameRegex();
}
