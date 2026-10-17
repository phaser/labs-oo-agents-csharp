// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/resolved.py

using System.Collections.ObjectModel;

namespace Nooa.Config;

/// <summary>
/// The loaded view of <c>secrets.yaml</c>: the <c>env:</c> map from variable name to value.
/// Upstream calls this type <c>Secrets</c>. The C# name differs, because <see cref="Nooa.Secrets"/> is the loader.
/// </summary>
/// <remarks>
/// Values are <see cref="SecretValue"/>, so printing a <see cref="ResolvedConfig"/> never leaks a key.
/// This is the loaded representation only. <see cref="Nooa.Secrets.LoadSecretsIntoEnv"/> pushes the values into the process environment.
/// </remarks>
public sealed record ResolvedSecrets
{
    /// <summary>The variable name to masked value.</summary>
    public IReadOnlyDictionary<string, SecretValue> Env { get; init; } = ReadOnlyDictionary<string, SecretValue>.Empty;

    /// <summary>Prints the variable names only.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append($"Env = [{string.Join(", ", Env.Keys)}]");
        return true;
    }
}
