// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

/// <summary>Sets process environment variables for a test and restores the old values on dispose. Use it only in the environment collection.</summary>
public sealed class EnvVars : IDisposable
{
    private readonly Dictionary<string, string?> _saved = [];

    public void Set(string name, string? value)
    {
        _saved.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
