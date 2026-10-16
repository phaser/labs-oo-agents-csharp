// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/layered_config.py

using System.Text;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Nooa;

/// <summary>
/// Shared layered-config discovery for every YAML config file (LLM aliases, settings, secrets).
/// </summary>
/// <remarks>
/// <para>Precedence, lowest to highest, last wins.</para>
/// <list type="number">
/// <item>Bundled defaults, supplied by the caller as <c>prepend</c> paths.</item>
/// <item>User: <c>~/.config/nooa/&lt;filename&gt;</c> (see <see cref="NooaPaths.GetUserDir"/>).</item>
/// <item>Project: <c>&lt;project-root&gt;/.nooa/&lt;filename&gt;</c> (see <see cref="NooaPaths.GetProjectDir"/>).</item>
/// <item>The paths in a named environment variable, comma separated. It has the highest priority, so a shell session can override files.</item>
/// </list>
/// <para>
/// Each path is canonicalized, so symbolic link aliases and repeated entries collapse. When the same path appears in
/// several layers, the lower-priority occurrence is dropped and the higher-priority position stays.
/// </para>
/// </remarks>
public static class LayeredConfig
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    /// <summary>
    /// Returns the existing config paths for <paramref name="filename"/>, lowest priority first.
    /// </summary>
    /// <param name="filename">Bare file name, looked up in the user and project directories (for example <c>llm_config.yaml</c>).</param>
    /// <param name="envVar">Environment variable with a comma-separated list of override paths. Null disables this layer.</param>
    /// <param name="prepend">Lowest-priority paths, already in priority order. Paths that do not exist are dropped.</param>
    /// <param name="projectDir">
    /// Explicit directory for the project layer. When null, <see cref="NooaPaths.GetProjectDir"/> is used.
    /// A host that serves several workspaces passes the active workspace directory.
    /// </param>
    /// <param name="logger">Receives a warning for an override path that does not exist.</param>
    public static IReadOnlyList<string> LayeredPaths(
        string filename,
        string? envVar = null,
        IEnumerable<string>? prepend = null,
        string? projectDir = null,
        ILogger? logger = null)
    {
        // Walk the layers in priority order. Keep the latest occurrence of any canonical path.
        var chain = new List<string>();

        void Push(string path)
        {
            chain.Remove(path);
            chain.Add(path);
        }

        foreach (var path in prepend ?? [])
        {
            if (ResolvedIfExists(path) is { } resolved)
            {
                Push(resolved);
            }
        }

        if (ResolvedIfExists(NooaPaths.GetUserDir(filename)) is { } user)
        {
            Push(user);
        }

        var projectPath = projectDir is not null ? Path.Combine(projectDir, filename) : NooaPaths.GetProjectDir(filename);
        if (ResolvedIfExists(projectPath) is { } project)
        {
            Push(project);
        }

        if (envVar is not null)
        {
            foreach (var raw in EnvPaths(envVar))
            {
                if (ResolvedIfExists(raw) is { } resolved)
                {
                    Push(resolved);
                }
                else
                {
                    logger?.LogWarning("{EnvVar} path does not exist: {Path}", envVar, raw);
                }
            }
        }

        return chain;
    }

    /// <summary>
    /// Loads every layer of <paramref name="filename"/> and deep-merges them into one dictionary.
    /// </summary>
    /// <remarks>
    /// Last wins. A null value deletes the key. A file that does not parse to a mapping is skipped with a warning,
    /// so one bad layer does not take down the rest. Mappings become <c>Dictionary&lt;string, object?&gt;</c>,
    /// sequences become <c>List&lt;object?&gt;</c>, and unquoted scalars become bool, integer, floating-point or string values.
    /// The warning text never includes file content, because the file can hold secrets.
    /// </remarks>
    public static Dictionary<string, object?> LoadLayeredYaml(
        string filename,
        string? envVar = null,
        IEnumerable<string>? prepend = null,
        string? projectDir = null,
        ILogger? logger = null)
    {
        var merged = new Dictionary<string, object?>();
        foreach (var path in LayeredPaths(filename, envVar, prepend, projectDir, logger))
        {
            object? data;
            try
            {
                data = ParseYaml(_strictUtf8.GetString(File.ReadAllBytes(path)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException or YamlException)
            {
                // Log the exception type only: a parser message can quote file content, and the file can hold secrets.
                logger?.LogWarning("Failed to load config file {Path}: {ErrorType}", path, e.GetType().Name);
                continue;
            }

            if (data is null)
            {
                continue;
            }

            if (data is not Dictionary<string, object?> mapping)
            {
                logger?.LogWarning("Config file {Path} is not a YAML mapping; skipping", path);
                continue;
            }

            DeepMerge(merged, mapping);
        }

        return merged;
    }

    /// <summary>
    /// Last-wins deep merge of <paramref name="overlay"/> onto <paramref name="baseMap"/>, which is changed in place.
    /// A null overlay value deletes the key. Two mappings merge recursively. Any other overlay value replaces the base value.
    /// </summary>
    private static void DeepMerge(Dictionary<string, object?> baseMap, Dictionary<string, object?> overlay)
    {
        foreach (var (key, value) in overlay)
        {
            if (value is null)
            {
                baseMap.Remove(key);
            }
            else if (value is Dictionary<string, object?> child)
            {
                var target = baseMap.GetValueOrDefault(key) as Dictionary<string, object?> ?? [];
                DeepMerge(target, child);
                baseMap[key] = target;
            }
            else
            {
                baseMap[key] = value;
            }
        }
    }

    /// <summary>Parses YAML text into the normalized object graph. Throws <see cref="YamlException"/> on invalid YAML.</summary>
    internal static object? ParseYaml(string text) => Normalize(_deserializer.Deserialize<object?>(text));

    private static object? Normalize(object? value) => value switch
    {
        IDictionary<object, object?> map => map.ToDictionary(e => e.Key.ToString() ?? string.Empty, e => Normalize(e.Value)),
        IList<object?> list => list.Select(Normalize).ToList(),
        _ => value,
    };

    private static string? ResolvedIfExists(string path)
    {
        try
        {
            var full = Path.GetFullPath(ExpandHome(path));
            return File.Exists(full) ? NooaPaths.Canonicalize(full) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Permission or stat failure: treat as missing.
            return null;
        }
    }

    private static IEnumerable<string> EnvPaths(string envVar)
    {
        var raw = Environment.GetEnvironmentVariable(envVar)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return [];
        }

        return raw.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0);
    }

    private static string ExpandHome(string path)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~', '/'));
        }

        return path;
    }
}
