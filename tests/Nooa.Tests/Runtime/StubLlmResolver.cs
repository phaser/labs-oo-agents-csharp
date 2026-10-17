// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Llm;

namespace Nooa.Tests.Runtime;

/// <summary>Test resolver: maps each alias to a fake client and counts the resolutions.</summary>
public sealed class StubLlmResolver : ILlmClientResolver
{
    public Dictionary<string, FakeLlmClient> Clients { get; } = [];

    public List<string> Requests { get; } = [];

    public ILlmClient Resolve(string alias)
    {
        Requests.Add(alias);
        return Clients.TryGetValue(alias, out var client) ? client : throw new KeyNotFoundException(alias);
    }
}
