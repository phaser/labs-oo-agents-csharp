// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/snapshot.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Nooa.Errors;
using Nooa.Json;

namespace Nooa.Storage;

/// <summary>
/// The serializable state of an agent: its context blocks, its disabled block keys and its field values.
/// </summary>
/// <remarks>
/// <para>
/// A snapshot file can be untrusted (decision D11). <see cref="Restore"/> checks the version first. It then
/// restores a field only when the field is declared on the agent class, its declared type is eligible
/// (see <see cref="SnapshotableAttribute"/>) and the declared type is in <see cref="TypeAllowlist"/>. The type
/// of a value always comes from the declaration and never from the file. All values are read before the first
/// write, so a bad file changes nothing.
/// </para>
/// <para>
/// A context block with a provider is not saved, because a delegate has no JSON form. The framework blocks
/// (<c>system_prompt</c>, <c>self</c>, <c>state</c>) are never saved and are never restored from a file.
/// A field or property with <see cref="NoSnapshotAttribute"/> is not saved. A field of a type that is not eligible
/// is skipped with a warning, so one bad field does not lose all other state.
/// </para>
/// </remarks>
public sealed record AgentSnapshot
{
    /// <summary>The snapshot format version this code reads and writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>The format version.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>The saved context blocks.</summary>
    public IReadOnlyList<SnapshotContextBlock> Context { get; init; } = [];

    /// <summary>The keys of the hidden context blocks.</summary>
    public IReadOnlyList<string> DisabledContext { get; init; } = [];

    /// <summary>The saved field values, by field name.</summary>
    public IReadOnlyDictionary<string, JsonNode?> Attributes { get; init; } = new Dictionary<string, JsonNode?>();

    /// <summary>The names of the declared types of the saved fields, sorted.</summary>
    public IReadOnlyList<string> TypeAllowlist { get; init; } = [];

    /// <summary>Reads the serializable state of an agent.</summary>
    /// <exception cref="SerializationException">A context block value is not JSON-like.</exception>
    public static AgentSnapshot FromAgent(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var manager = agent.ContextManager;
        var blocks = new List<SnapshotContextBlock>();
        foreach (var (key, entry) in manager.RawItems())
        {
            if (manager.IsProtected(key))
            {
                continue;
            }

            if (entry.IsDynamic)
            {
                agent.Logger.LogWarning("Snapshot: skipping the context block {Key}, because it has a provider", key);
                continue;
            }

            try
            {
                blocks.Add(new SnapshotContextBlock(key, SnapshotValues.ToNode(entry.Value), entry.IsStatic));
            }
            catch (SerializationException ex)
            {
                throw new SerializationException($"Context block '{key}' is not serializable: {ex.Message}", ex);
            }
        }

        var attributes = new SortedDictionary<string, JsonNode?>(StringComparer.Ordinal);
        var allowlist = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (name, member) in SnapshotTypes.MembersOf(agent.GetType()))
        {
            var value = member.Field.GetValue(agent);
            if (SnapshotTypes.IsNoSnapshotValue(value))
            {
                continue;
            }

            if (!SnapshotTypes.IsEligible(member.Type))
            {
                agent.Logger.LogWarning(
                    "Snapshot: skipping the field {Field} of type {Type}. Mark it [NoSnapshot], or use a record or a [Snapshotable] type",
                    name,
                    member.Type.Name);
                continue;
            }

            try
            {
                attributes[name] = JsonSerializer.SerializeToNode(value, member.Type, NooaJson.Options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                agent.Logger.LogWarning(ex, "Snapshot: skipping the field {Field}, which is not serializable", name);
                continue;
            }

            allowlist.Add(SnapshotTypes.NameOf(member.Type));
        }

        return new AgentSnapshot
        {
            Context = blocks,
            DisabledContext = [.. manager.Disabled().Order(StringComparer.Ordinal)],
            Attributes = attributes,
            TypeAllowlist = [.. allowlist],
        };
    }

    /// <summary>
    /// Restores this snapshot into an agent. The restore is additive: it does not clear the blocks or fields that the
    /// snapshot does not mention. Use a freshly built agent.
    /// </summary>
    /// <exception cref="SerializationException">The version is not <see cref="CurrentVersion"/>.</exception>
    /// <exception cref="DeserializationException">A field type is not in the allowlist, a value does not fit the declared type, or a block value is not JSON-like.</exception>
    public void Restore(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (Version != CurrentVersion)
        {
            throw new SerializationException($"Snapshot version mismatch: expected {CurrentVersion}, got {Version}");
        }

        var members = SnapshotTypes.MembersOf(agent.GetType());
        var allowed = TypeAllowlist.ToHashSet(StringComparer.Ordinal);
        var values = new List<(SnapshotTypes.SnapshotMember Member, object? Value)>();
        foreach (var (name, node) in Attributes)
        {
            if (!members.TryGetValue(name, out var member) || !SnapshotTypes.IsEligible(member.Type))
            {
                agent.Logger.LogWarning("Snapshot: ignoring the field {Field}, which the agent does not declare as snapshot state", name);
                continue;
            }

            var typeName = SnapshotTypes.NameOf(member.Type);
            if (!allowed.Contains(typeName))
            {
                throw new DeserializationException(
                    $"Type '{typeName}' of field '{name}' is not in the allowlist. The snapshot may have been tampered with.");
            }

            object? value;
            try
            {
                value = node.Deserialize(member.Type, NooaJson.Options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                throw new DeserializationException($"The value of field '{name}' does not fit type '{member.Type.Name}'.", ex);
            }

            if (value is null && member.Type.IsValueType && Nullable.GetUnderlyingType(member.Type) is null)
            {
                throw new DeserializationException($"The value of field '{name}' is null, but type '{member.Type.Name}' is not nullable.");
            }

            values.Add((member, value));
        }

        var blocks = new List<(SnapshotContextBlock Block, object? Value)>();
        foreach (var block in Context)
        {
            if (agent.ContextManager.IsProtected(block.Key))
            {
                agent.Logger.LogWarning("Snapshot: ignoring the protected context block {Key}", block.Key);
                continue;
            }

            blocks.Add((block, SnapshotValues.FromNode(block.Value)));
        }

        foreach (var (block, value) in blocks)
        {
            if (block.IsStatic && value is not null)
            {
                agent.ContextManager.SetStatic(block.Key, value);
            }
            else
            {
                agent.ContextManager.Set(block.Key, value);
            }
        }

        var disabled = new List<string>();
        foreach (var key in DisabledContext)
        {
            if (agent.ContextManager.IsProtected(key))
            {
                agent.Logger.LogWarning("Snapshot: ignoring the disabled key {Key}, which names a protected context block", key);
                continue;
            }

            disabled.Add(key);
        }

        agent.ContextManager.Disable([.. disabled]);
        foreach (var (member, value) in values)
        {
            member.Field.SetValue(agent, value);
        }
    }
}
