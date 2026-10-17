// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tools;

namespace Nooa.Tests.Tools;

/// <summary>A temporary directory for shell tests, with its canonical path (macOS temp paths hold symbolic links). Deleted on dispose.</summary>
public sealed class ShellTestDirectory : IDisposable
{
    public ShellTestDirectory()
    {
        Path = NooaPaths.Canonicalize(Directory.CreateTempSubdirectory("nooa-shell-test-").FullName);
    }

    public string Path { get; }

    public ShellOptions Options(string? initCommand = null, bool keepBackground = false, IReadOnlyDictionary<string, string>? extra = null)
        => new() { Cwd = Path, InitCommand = initCommand, KeepBackgroundOnClose = keepBackground, ExtraEnvironment = extra };

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A test left a process that writes here. The temp directory is cleaned by the OS.
        }
    }
}
