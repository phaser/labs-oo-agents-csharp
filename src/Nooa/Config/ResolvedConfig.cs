// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/resolved.py

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Nooa.Llm;

namespace Nooa.Config;

/// <summary>
/// Everything the three layered config files resolve to, as typed objects.
/// </summary>
/// <remarks>
/// <see cref="Load"/> is the programmatic form of <c>nooa config show</c>.
/// The values of <see cref="Secrets"/> are masked on print.
/// </remarks>
public sealed record ResolvedConfig
{
    private const string _settingsFile = "settings.yaml";
    private const string _settingsEnvVar = "NEMO_OO_SETTINGS";

    /// <summary>Alias to model config, from <c>llm_config.yaml</c> and the bundled defaults, after the layer trust rules.</summary>
    public IReadOnlyDictionary<string, ModelConfig> Models { get; init; } = ReadOnlyDictionary<string, ModelConfig>.Empty;

    /// <summary>The <c>env:</c> view of <c>secrets.yaml</c>. Values are masked.</summary>
    public ResolvedSecrets Secrets { get; init; } = new();

    /// <summary>The merged <c>settings.yaml</c> mapping. It stays a dictionary, because the settings object belongs to the CLI package.</summary>
    public IReadOnlyDictionary<string, object?> Settings { get; init; } = ReadOnlyDictionary<string, object?>.Empty;

    /// <summary>The file paths that contributed to each file kind, lowest priority first.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Sources { get; init; } = ReadOnlyDictionary<string, IReadOnlyList<string>>.Empty;

    /// <summary>
    /// Reads the currently active config across all layers.
    /// </summary>
    /// <param name="bundledLlmConfigPaths">The bundled default <c>llm_config.yaml</c> files, lowest priority first.</param>
    /// <param name="logger">Receives warnings. Warnings never include file content.</param>
    public static ResolvedConfig Load(IEnumerable<string>? bundledLlmConfigPaths = null, ILogger? logger = null)
    {
        var bundled = bundledLlmConfigPaths?.ToList();
        var settings = LayeredConfig.LoadLayeredYaml(_settingsFile, _settingsEnvVar, logger: logger);

        var secretsRaw = LayeredConfig.LoadLayeredYaml(Nooa.Secrets.FileName, Nooa.Secrets.EnvVar, logger: logger);
        var env = secretsRaw.GetValueOrDefault("env") is Dictionary<string, object?> envRaw
            ? envRaw.ToDictionary(kv => kv.Key, kv => new SecretValue(Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? ""), StringComparer.Ordinal)
            : [];

        var registry = ModelRegistry.Load(bundled, logger);
        return new ResolvedConfig
        {
            Models = registry.Configs,
            Secrets = new ResolvedSecrets { Env = env },
            Settings = settings,
            Sources = new Dictionary<string, IReadOnlyList<string>>
            {
                ["settings"] = LayeredConfig.LayeredPaths(_settingsFile, _settingsEnvVar, logger: logger),
                ["secrets"] = LayeredConfig.LayeredPaths(Nooa.Secrets.FileName, Nooa.Secrets.EnvVar, logger: logger),
                ["llm_config"] = LlmConfig.Chain(bundled, logger),
            },
        };
    }
}
