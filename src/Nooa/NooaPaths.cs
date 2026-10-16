// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/paths.py

namespace Nooa;

/// <summary>
/// Well-known filesystem paths.
/// </summary>
/// <remarks>
/// <para>
/// The user directory is <c>~/.config/nooa/</c> on every platform. It honors <c>XDG_CONFIG_HOME</c> when set.
/// The environment variable <c>NEMO_OO_USER_DIR</c> overrides the whole base.
/// </para>
/// <para>
/// The project directory is <c>&lt;project-root&gt;/.nooa/</c>. The environment variable <c>NEMO_OO_PROJECT_DIR</c> overrides it.
/// </para>
/// </remarks>
public static class NooaPaths
{
    /// <summary>Directory name of the project-local directory.</summary>
    public const string DirName = ".nooa";

    /// <summary>Environment variable that overrides the user directory.</summary>
    public const string UserDirEnvVar = "NEMO_OO_USER_DIR";

    /// <summary>Environment variable that overrides the project directory.</summary>
    public const string ProjectDirEnvVar = "NEMO_OO_PROJECT_DIR";

    private const int _maxLinkDepth = 40;

    /// <summary>
    /// Finds the project root: the nearest directory, from <paramref name="startDirectory"/> upward,
    /// that holds a <c>.nooa</c> directory or a <c>.git</c> entry.
    /// Falls back to <paramref name="startDirectory"/> when none is found.
    /// </summary>
    /// <param name="startDirectory">The directory to start from. The default is the current directory.</param>
    public static string FindProjectRoot(string? startDirectory = null)
    {
        var start = Path.GetFullPath(startDirectory ?? Directory.GetCurrentDirectory());
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, DirName)) || Path.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
        }

        return start;
    }

    /// <summary>Returns the user-global directory, joined with <paramref name="parts"/>.</summary>
    public static string GetUserDir(params string[] parts)
    {
        var overrideDir = Environment.GetEnvironmentVariable(UserDirEnvVar);
        string baseDir;
        if (!string.IsNullOrEmpty(overrideDir))
        {
            baseDir = overrideDir;
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var configHome = !string.IsNullOrEmpty(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            baseDir = Path.Combine(configHome, "nooa");
        }

        return Join(baseDir, parts);
    }

    /// <summary>Returns the project-local directory, joined with <paramref name="parts"/>.</summary>
    public static string GetProjectDir(params string[] parts)
    {
        var overrideDir = Environment.GetEnvironmentVariable(ProjectDirEnvVar);
        var baseDir = !string.IsNullOrEmpty(overrideDir) ? overrideDir : Path.Combine(FindProjectRoot(), DirName);
        return Join(baseDir, parts);
    }

    /// <summary>
    /// Returns the absolute path with every symbolic link resolved, in every path component.
    /// A path that does not exist is resolved as far as it exists.
    /// </summary>
    public static string Canonicalize(string path)
    {
        var current = Path.GetFullPath(path);
        for (var depth = 0; depth < _maxLinkDepth; depth++)
        {
            var root = Path.GetPathRoot(current)!;
            var resolved = root;
            var changed = false;
            foreach (var segment in current[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                var next = Path.Combine(resolved, segment);
                FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
                var target = info.LinkTarget is null ? null : info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    next = target.FullName;
                    changed = true;
                }

                resolved = next;
            }

            if (!changed)
            {
                return resolved;
            }

            current = Path.GetFullPath(resolved);
        }

        throw new IOException($"Too many levels of symbolic links: {path}");
    }

    private static string Join(string baseDir, string[] parts) => parts.Length == 0 ? baseDir : Path.Combine([baseDir, .. parts]);
}
