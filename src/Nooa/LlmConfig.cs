// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_config.py

using Microsoft.Extensions.Logging;

namespace Nooa;

/// <summary>
/// Config-file discovery for the LLM registry.
/// </summary>
public static class LlmConfig
{
    /// <summary>Name of the LLM registry config file.</summary>
    public const string FileName = "llm_config.yaml";

    /// <summary>Environment variable with comma-separated override paths.</summary>
    public const string EnvVar = "NEMO_OO_LLM_CONFIG";

    /// <summary>
    /// Returns the YAML config files for the LLM registry, lowest priority first.
    /// </summary>
    /// <remarks>
    /// <para>The layers, in order, are:</para>
    /// <list type="number">
    /// <item><paramref name="bundledPaths"/>, the bundled defaults.</item>
    /// <item><c>~/.config/nooa/llm_config.yaml</c>, the user-global config.</item>
    /// <item><c>&lt;project-root&gt;/.nooa/llm_config.yaml</c>, the project-local config.</item>
    /// <item>The paths in <c>NEMO_OO_LLM_CONFIG</c>, the highest priority.</item>
    /// </list>
    /// <para>
    /// Upstream finds bundled defaults through Python entry points. Plugin discovery is not in v1 (decision D14),
    /// so the caller passes the bundled file paths. Only files that exist are returned.
    /// </para>
    /// </remarks>
    /// <param name="bundledPaths">Lowest-priority bundled default files, in priority order.</param>
    /// <param name="logger">Receives a warning for an override path that does not exist.</param>
    public static IReadOnlyList<string> Chain(IEnumerable<string>? bundledPaths = null, ILogger? logger = null) =>
        LayeredConfig.LayeredPaths(FileName, EnvVar, prepend: bundledPaths, logger: logger);
}
