// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/registry.py

using System.Collections.ObjectModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>
/// The model registry: alias to <see cref="ModelConfig"/>, built from the layered <c>llm_config.yaml</c> files.
/// It resolves an alias to an <see cref="ILlmClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// The registry is an immutable snapshot. To reload, call <see cref="Load"/> again. Upstream keeps a global mutable
/// <c>MODELS</c> dictionary with a lock. The snapshot needs no lock.
/// Layers merge last-wins. An alias set to null in a later layer is removed.
/// </para>
/// <para>
/// Trust rule (deviation from upstream). A project-layer file can come from a cloned repository, so it is untrusted
/// (see <see cref="LayerTrust"/>). Without this rule such a file could send an existing API key to another host, or
/// send the prompts of a trusted alias to another host.
/// </para>
/// <list type="bullet">
/// <item>An untrusted layer cannot replace or remove an alias that a trusted layer defined. The entry is ignored and a warning names the alias.</item>
/// <item>An untrusted layer never sets <c>api_key_env</c>. The field is dropped with a warning. An alias that only untrusted layers define has no key, unless it uses the default OpenAI host and <c>OPENAI_API_KEY</c>. A route prefix that implies a host (<c>nvidia_nim/</c>) also gets no key.</item>
/// </list>
/// <para>
/// A key is sent only to the host that the alias names. No key falls back to another variable when the named
/// variable is unset. The call fails instead (upstream falls back to <c>OPENAI_API_KEY</c>, which sends that key to a custom host).
/// Keys never appear in logs or in exception messages.
/// </para>
/// </remarks>
public sealed class ModelRegistry : ILlmClientResolver
{
    private const string _defaultKeyEnv = "OPENAI_API_KEY";
    private const string _unusedKey = "unused";

    // NVIDIA_INTERNAL_API_KEY was renamed to NVIDIA_INFERENCE_API_KEY. Either name works as a fallback for the other.
    private static readonly Dictionary<string, string> _nvidiaKeySynonyms = new(StringComparer.Ordinal)
    {
        ["NVIDIA_INFERENCE_API_KEY"] = "NVIDIA_INTERNAL_API_KEY",
        ["NVIDIA_INTERNAL_API_KEY"] = "NVIDIA_INFERENCE_API_KEY",
    };

    private readonly Func<string, string?, string, IChatClient>? _chatClientFactory;
    private readonly ILogger? _logger;

    private ModelRegistry(
        IReadOnlyDictionary<string, ModelConfig> configs,
        ILogger? logger,
        Func<string, string?, string, IChatClient>? chatClientFactory)
    {
        Configs = configs;
        _logger = logger;
        _chatClientFactory = chatClientFactory;
    }

    /// <summary>The configured aliases.</summary>
    public IReadOnlyDictionary<string, ModelConfig> Configs { get; }

    /// <summary>
    /// Loads the registry from the layer chain of <see cref="LlmConfig.Chain"/>.
    /// </summary>
    /// <param name="bundledPaths">The bundled default files, lowest priority first. They are trusted.</param>
    /// <param name="logger">Receives warnings. Warnings never include file content or keys.</param>
    /// <param name="chatClientFactory">Builds the chat client from the wire model name, the base URL (or null) and the key. Null builds the OpenAI client. Tests use it to avoid the network.</param>
    public static ModelRegistry Load(
        IEnumerable<string>? bundledPaths = null,
        ILogger? logger = null,
        Func<string, string?, string, IChatClient>? chatClientFactory = null)
    {
        var bundled = bundledPaths?.ToList();
        var bundledSet = (bundled ?? []).Select(NooaPaths.Canonicalize).ToList();
        var layers = LayeredConfig.LoadLayers(LlmConfig.FileName, LlmConfig.EnvVar, bundled, logger: logger)
            .Select(l => (l.Path, (IReadOnlyDictionary<string, object?>)l.Data,
                bundledSet.Any(b => NooaPaths.PathEquals(b, l.Path)) || LayerTrust.IsTrusted(l.Path, LlmConfig.FileName, LlmConfig.EnvVar)))
            .ToList();
        return Build(layers, logger, chatClientFactory);
    }

    /// <summary>Merges the layers, lowest priority first, with the trust rule.</summary>
    internal static ModelRegistry Build(
        IEnumerable<(string Path, IReadOnlyDictionary<string, object?> Data, bool Trusted)> layers,
        ILogger? logger = null,
        Func<string, string?, string, IChatClient>? chatClientFactory = null)
    {
        var configs = new Dictionary<string, ModelConfig>(StringComparer.Ordinal);
        var definedByTrusted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, data, trusted) in layers)
        {
            if (!data.TryGetValue("models", out var modelsValue) || modelsValue is null)
            {
                continue;
            }

            if (modelsValue is not IReadOnlyDictionary<string, object?> models)
            {
                logger?.LogWarning("'models' is not a mapping in {Path}; ignoring", path);
                continue;
            }

            foreach (var (name, entry) in models)
            {
                if (!trusted && definedByTrusted.Contains(name))
                {
                    logger?.LogWarning("Model {Alias} in untrusted file {Path} cannot replace or remove a model from a trusted layer; ignoring", name, path);
                    continue;
                }

                configs.Remove(name);
                if (trusted)
                {
                    definedByTrusted.Remove(name);
                }

                if (entry is null)
                {
                    continue;
                }

                if (entry is not IReadOnlyDictionary<string, object?> raw)
                {
                    logger?.LogWarning("Ignoring model {Alias} in {Path}: expected mapping or null, got {Type}", name, path, entry.GetType().Name);
                    continue;
                }

                if (!trusted)
                {
                    raw = SanitizeUntrusted(name, raw, logger);
                }

                try
                {
                    configs[name] = ModelConfig.FromRegistry(name, raw);
                    if (trusted)
                    {
                        definedByTrusted.Add(name);
                    }
                }
                catch (ConfigValidationException e)
                {
                    logger?.LogWarning("Ignoring model {Alias} in {Path}: {Reason}", name, path, e.Message);
                }
            }
        }

        return new ModelRegistry(new ReadOnlyDictionary<string, ModelConfig>(configs), logger, chatClientFactory);
    }

    /// <summary>Returns the typed config of <paramref name="alias"/>, or null when it is not configured.</summary>
    public ModelConfig? GetModelConfig(string alias) => Configs.GetValueOrDefault(alias);

    /// <inheritdoc />
    public ILlmClient Resolve(string alias) => Create(alias);

    /// <summary>
    /// Builds the client for <paramref name="alias"/>.
    /// </summary>
    /// <param name="alias">The configured alias.</param>
    /// <param name="reasoningLevel">The reasoning level to request. Null uses <c>reasoning_level</c> of the alias, or none.</param>
    /// <exception cref="KeyNotFoundException">No model is configured under <paramref name="alias"/>.</exception>
    /// <exception cref="InvalidOperationException">The alias is a decision model.</exception>
    /// <exception cref="NotSupportedException">The alias needs a client kind that is not ported (Responses API, backlog B09).</exception>
    /// <exception cref="ConfigValidationException">The alias is malformed, or the environment variable of its key is unset.</exception>
    public ILlmClient Create(string alias, string? reasoningLevel = null)
    {
        var config = GetModelConfig(alias) ?? throw new KeyNotFoundException($"No model is configured under alias '{alias}'");
        switch (config.ClientType ?? "completion")
        {
            case "completion":
                break;
            case "decision":
                throw new InvalidOperationException($"Model alias '{alias}' has client_type 'decision' and cannot be used as an LLM");
            case var other:
                throw new NotSupportedException($"Model alias '{alias}' has unsupported client_type '{other}'");
        }

        var (model, defaultBase) = OpenAiChatClientFactory.ParseModel(config.ModelName ?? alias);
        var apiBase = config.ApiBase ?? defaultBase;
        var apiKey = ResolveApiKey(alias, config, hasCustomRoute: apiBase is not null);
        _logger?.LogDebug("LLM registry hit for {Alias}: model {Model}, host {Host}", alias, model, apiBase is null ? "default" : new Uri(apiBase).Authority);

        var chat = _chatClientFactory is null
            ? OpenAiChatClientFactory.Create(model, apiBase, apiKey)
            : _chatClientFactory(model, apiBase, apiKey);
        var options = BuildOptions(alias, config, model, reasoningLevel);
        return new ChatClientLlmClient(chat, options, logger: _logger);
    }

    // An untrusted layer never selects an API key: a key that no trusted layer named is not sent to any host.
    private static IReadOnlyDictionary<string, object?> SanitizeUntrusted(string name, IReadOnlyDictionary<string, object?> raw, ILogger? logger)
    {
        if (!raw.ContainsKey("api_key_env"))
        {
            return raw;
        }

        logger?.LogWarning("Model {Alias} in an untrusted file: api_key_env is dropped; only a trusted layer can select an API key", name);
        return raw.Where(kv => kv.Key != "api_key_env").ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    private string ResolveApiKey(string alias, ModelConfig config, bool hasCustomRoute)
    {
        if (config.ApiKeyEnv is { } name)
        {
            var value = ReadEnv(name) ?? (_nvidiaKeySynonyms.TryGetValue(name, out var synonym) ? ReadEnv(synonym) : null);
            return value ?? throw new ConfigValidationException(
                $"Model '{alias}' is configured to read its API key from environment variable '{name}', but that variable is unset or empty");
        }

        if (hasCustomRoute)
        {
            return _unusedKey;
        }

        return ReadEnv(_defaultKeyEnv) ?? throw new ConfigValidationException(
            $"Model '{alias}' has no api_key_env and no api_base, so it uses the default OpenAI endpoint, but {_defaultKeyEnv} is unset or empty");
    }

    private static string? ReadEnv(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    private ChatClientLlmOptions BuildOptions(string alias, ModelConfig config, string model, string? reasoningLevel)
    {
        var defaults = new Dictionary<string, object?>(StringComparer.Ordinal);
        void Set(string key, object? value)
        {
            if (value is not null)
            {
                defaults[key] = value;
            }
        }

        Set("temperature", config.Temperature);
        Set("top_p", config.TopP);
        Set("max_tokens", config.MaxTokens);
        foreach (var key in new[] { "reasoning", "reasoning_effort", "extra_body" })
        {
            Set(key, config.Extra.GetValueOrDefault(key));
        }

        var declaration = new ReasoningConfig { Levels = config.ReasoningLevels, Default = config.ReasoningDefault };
        declaration.Validate();
        var selection = reasoningLevel ?? config.ReasoningLevel;
        IReadOnlyDictionary<string, object?> parameters;
        try
        {
            parameters = declaration.Apply(defaults, selection);
        }
        catch (ArgumentException e)
        {
            throw new ConfigValidationException($"Model '{alias}': {e.Message}");
        }

        var options = new ChatClientLlmOptions
        {
            Model = model,
            ContextWindow = config.ContextWindow,
            Retry = ParseRetry(alias, config.Extra),
        };
        var unsupported = new List<string>();
        foreach (var (key, value) in parameters)
        {
            switch (key)
            {
                case "temperature":
                    options = options with { Temperature = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture) };
                    break;
                case "top_p":
                    options = options with { TopP = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture) };
                    break;
                case var cap when ContextLimits.ReplyCapKeys.Contains(cap):
                    options = options with { MaxOutputTokens = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) };
                    break;
                case "reasoning_effort":
                    options = options with { ReasoningEffort = ParseEffort(alias, value) };
                    break;
                default:
                    unsupported.Add(key);
                    break;
            }
        }

        if (unsupported.Count > 0)
        {
            _logger?.LogWarning("Model {Alias}: the chat client does not send these settings: {Keys}", alias, string.Join(", ", unsupported));
        }

        options.Validate();
        return options;
    }

    private static ReasoningEffort ParseEffort(string alias, object? value)
    {
        var text = (value as string)?.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return text is not null && Enum.TryParse<ReasoningEffort>(text, ignoreCase: true, out var effort)
            ? effort
            : text == "xhigh" ? ReasoningEffort.ExtraHigh
            : throw new ConfigValidationException($"Model '{alias}' has an invalid reasoning_effort: expected none, low, medium, high or extra_high");
    }

    // retry_config: false or null makes one attempt. A mapping sets the RetryOptions fields. Absent keeps the defaults.
    internal static RetryOptions? ParseRetry(string alias, IReadOnlyDictionary<string, object?> extra)
    {
        if (!extra.TryGetValue("retry_config", out var value))
        {
            return new RetryOptions();
        }

        if (value is null or false)
        {
            return new RetryOptions { MaxRetries = 0, RateLimitExtraRetries = 0 };
        }

        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            throw new ConfigValidationException($"Model '{alias}' retry_config must be a mapping, false or null");
        }

        var retry = new RetryOptions();
        foreach (var (key, v) in map)
        {
            var number = v is null ? double.NaN : ModelConfig.ToDouble(v) ?? double.NaN;
            retry = key switch
            {
                "max_retries" when !double.IsNaN(number) => retry with { MaxRetries = (int)number },
                "base_delay" when !double.IsNaN(number) => retry with { BaseDelay = TimeSpan.FromSeconds(number) },
                "max_delay" when !double.IsNaN(number) => retry with { MaxDelay = TimeSpan.FromSeconds(number) },
                "exponential_base" when !double.IsNaN(number) => retry with { ExponentialBase = number },
                "jitter_factor" when !double.IsNaN(number) => retry with { JitterFactor = number },
                "rate_limit_extra_retries" when !double.IsNaN(number) => retry with { RateLimitExtraRetries = (int)number },
                "rate_limit_base_delay" when !double.IsNaN(number) => retry with { RateLimitBaseDelay = TimeSpan.FromSeconds(number) },
                "rate_limit_backoff_base" when !double.IsNaN(number) => retry with { RateLimitBackoffBase = number },
                "retry_on_empty_content" when v is bool b => retry with { RetryOnEmptyContent = b },
                _ => throw new ConfigValidationException($"Model '{alias}' retry_config has an unknown or invalid key '{key}'"),
            };
        }

        return retry;
    }
}
