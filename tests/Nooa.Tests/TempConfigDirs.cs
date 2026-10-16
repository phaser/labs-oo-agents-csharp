// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

/// <summary>
/// Creates a temporary root with user and project directories and points the Nooa path environment variables at them.
/// It restores the environment variables and deletes the root on dispose. Use it only in the environment collection.
/// </summary>
public sealed class TempConfigDirs : IDisposable
{
    private static readonly string[] _variables =
    [
        NooaPaths.UserDirEnvVar,
        NooaPaths.ProjectDirEnvVar,
        "XDG_CONFIG_HOME",
        LlmConfig.EnvVar,
        Secrets.EnvVar,
        "NEMO_OO_SETTINGS",
    ];

    private readonly Dictionary<string, string?> _saved = [];

    public TempConfigDirs()
    {
        foreach (var name in _variables)
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        Root = Directory.CreateTempSubdirectory("nooa-tests-").FullName;
        UserDir = Directory.CreateDirectory(Path.Combine(Root, "user")).FullName;
        ProjectDir = Directory.CreateDirectory(Path.Combine(Root, "project")).FullName;
        Environment.SetEnvironmentVariable(NooaPaths.UserDirEnvVar, UserDir);
        Environment.SetEnvironmentVariable(NooaPaths.ProjectDirEnvVar, ProjectDir);
    }

    public string Root { get; }

    public string UserDir { get; }

    public string ProjectDir { get; }

    public static void Write(string path, string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        Directory.Delete(Root, recursive: true);
    }
}
