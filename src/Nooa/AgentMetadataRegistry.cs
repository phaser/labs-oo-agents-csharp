// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace Nooa;

/// <summary>
/// Holds the <see cref="AgentMetadata"/> that generated module initializers register.
/// </summary>
public static class AgentMetadataRegistry
{
    private static readonly ConcurrentDictionary<Type, AgentMetadata> _items = new();

    /// <summary>Adds or replaces the metadata of one agent type.</summary>
    public static void Register(AgentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        _items[metadata.AgentType] = metadata;
    }

    /// <summary>Finds the metadata of an agent type. Returns false when the type has none.</summary>
    public static bool TryGet(Type agentType, out AgentMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(agentType);
        return _items.TryGetValue(agentType, out metadata);
    }
}
