// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/secrets.py

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Nooa;

/// <summary>
/// Secrets loading: <c>secrets.yaml</c> to process environment variables, without overwriting.
/// </summary>
/// <remarks>
/// <para>
/// The file has one <c>env:</c> mapping from environment variable name to value. It is discovered through
/// <see cref="LayeredConfig"/> (user, then project, then the <c>NEMO_OO_SECRETS</c> paths).
/// </para>
/// <para>
/// Secret values never go to logs, traces or exception messages. Only variable names appear in diagnostics.
/// </para>
/// </remarks>
public static partial class Secrets
{
    /// <summary>Name of the secrets file.</summary>
    public const string FileName = "secrets.yaml";

    /// <summary>Environment variable with comma-separated override paths.</summary>
    public const string EnvVar = "NEMO_OO_SECRETS";

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Loads the layered secrets file and sets its <c>env:</c> entries as process environment variables.
    /// </summary>
    /// <remarks>
    /// A name that is already set in the process is left alone: the shell value wins. A null value is skipped.
    /// Other values are converted to text. The call is safe to repeat.
    /// </remarks>
    /// <returns>The names that this call set, that is, the names that were missing.</returns>
    public static IReadOnlyList<string> LoadSecretsIntoEnv(ILogger? logger = null)
    {
        var merged = LayeredConfig.LoadLayeredYaml(FileName, EnvVar, logger: logger);
        if (!merged.TryGetValue("env", out var envValue) || envValue is null)
        {
            return [];
        }

        if (envValue is not Dictionary<string, object?> envMap)
        {
            logger?.LogWarning("secrets.yaml `env:` is not a mapping ({Type}); ignoring", envValue.GetType().Name);
            return [];
        }

        var applied = new List<string>();
        foreach (var (name, value) in envMap)
        {
            if (value is null || Environment.GetEnvironmentVariable(name) is not null)
            {
                // A process value wins. Never overwrite it.
                continue;
            }

            Environment.SetEnvironmentVariable(name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            applied.Add(name);
        }

        return applied;
    }

    /// <summary>
    /// Saves one credential atomically with owner-only permissions.
    /// </summary>
    /// <remarks>
    /// The call does not change the process environment. The caller owns user consent and must explain that an exported
    /// shell value still takes priority. No error message includes file content.
    /// </remarks>
    /// <exception cref="ArgumentException">The name is not a valid environment variable name, or the value is empty.</exception>
    /// <exception cref="InvalidDataException">The file is not valid YAML, has no <c>env</c> mapping, or changed during the write.</exception>
    public static void WriteSecretEnv(string path, string name, string value)
    {
        if (!EnvNameRegex().IsMatch(name))
        {
            throw new ArgumentException("Secret variable must be a valid environment variable name", nameof(name));
        }

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Secret value cannot be empty", nameof(value));
        }

        path = NooaPaths.Canonicalize(path);
        var original = File.Exists(path) ? File.ReadAllText(path) : null;

        object? parsed = null;
        var invalidYaml = false;
        if (original is not null)
        {
            try
            {
                parsed = LayeredConfig.ParseYaml(original);
            }
            catch (YamlException)
            {
                // Do not chain the parser error: it can quote secret text from the file.
                invalidYaml = true;
            }
        }

        if (invalidYaml)
        {
            throw new InvalidDataException($"Secrets file {path} contains invalid YAML; no changes made");
        }

        if (parsed is not null and not Dictionary<string, object?>)
        {
            throw new InvalidDataException($"Secrets file {path} must contain an env mapping");
        }

        var data = parsed as Dictionary<string, object?> ?? [];
        if (data.GetValueOrDefault("env") is null)
        {
            data["env"] = new Dictionary<string, object?>();
        }

        if (data["env"] is not Dictionary<string, object?> env)
        {
            throw new InvalidDataException($"Secrets file {path} must contain an env mapping");
        }

        env[name] = value;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            using (var writer = new StreamWriter(stream, _utf8NoBom))
            {
                writer.Write(new SerializerBuilder().WithQuotingNecessaryStrings().Build().Serialize(data));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if ((File.Exists(path) ? File.ReadAllText(path) : null) != original)
            {
                throw new InvalidDataException($"Secrets file {path} changed during write; retry after reloading");
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvNameRegex();
}
