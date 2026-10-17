// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/persistent_vars.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Json;

namespace Nooa.Storage;

/// <summary>
/// Durable named state over an existing JSON dictionary. It is a view, not a store: it shares the dictionary of its owner,
/// so a value that the view writes is a value that the owner snapshot keeps.
/// </summary>
/// <remarks>
/// <para>Choose the scope on purpose:</para>
/// <list type="bullet">
/// <item>Variables of the agent: identity, stable preferences, environment facts and coordination that last across tasks and sessions.</item>
/// <item><see cref="Nooa.Tools.Todo.V"/>: plans, findings, artifacts and checkpoints of one task.</item>
/// <item>Variables of a cell: scratch data that needs no owner.</item>
/// </list>
/// <para>
/// Values are JSON (D11). <see cref="Set"/> stores null, text, numbers, booleans, lists and dictionaries with text keys.
/// A value that has no JSON form, such as a live client or a delegate, is not stored and <see cref="Set"/> returns false.
/// Upstream gives the view attribute access (<c>self.v.name</c>). C# has no such access without <c>dynamic</c>, so use
/// <see cref="Set"/>, <see cref="Get{T}"/> and the indexer.
/// </para>
/// </remarks>
public sealed class PersistentVars
{
    private readonly IDictionary<string, JsonNode?> _vars;

    /// <summary>Creates a view over <paramref name="vars"/>. The view writes to this dictionary.</summary>
    public PersistentVars(IDictionary<string, JsonNode?> vars)
    {
        ArgumentNullException.ThrowIfNull(vars);
        _vars = vars;
    }

    /// <summary>The number of stored names.</summary>
    public int Count => _vars.Count;

    /// <summary>Returns a stored value as JSON.</summary>
    /// <exception cref="KeyNotFoundException">The name is not stored.</exception>
    public JsonNode? this[string key] =>
        _vars.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"No var '{key}'");

    /// <summary>True when the name is stored.</summary>
    public bool Contains(string key) => _vars.ContainsKey(key);

    /// <summary>Returns the stored names.</summary>
    public IReadOnlyList<string> Keys() => [.. _vars.Keys];

    /// <summary>Returns the stored names with their values, for inspection.</summary>
    public IReadOnlyList<KeyValuePair<string, JsonNode?>> Items() => [.. _vars];

    /// <summary>Returns a stored value as <typeparamref name="T"/>, or <paramref name="defaultValue"/> when the name is not stored.</summary>
    public T? Get<T>(string key, T? defaultValue = default) =>
        _vars.TryGetValue(key, out var value) ? value is null ? default : value.Deserialize<T>(NooaJson.Options) : defaultValue;

    /// <summary>Stores a value. Returns false, and stores nothing, when the value has no JSON form.</summary>
    public bool Set(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            _vars[key] = SnapshotValues.ToNode(value);
            return true;
        }
        catch (Nooa.Errors.SerializationException)
        {
            return false;
        }
    }

    /// <summary>Removes a stored name.</summary>
    /// <exception cref="KeyNotFoundException">The name is not stored.</exception>
    public void Remove(string key)
    {
        if (!_vars.Remove(key))
        {
            throw new KeyNotFoundException($"No var '{key}'");
        }
    }

    /// <summary>Removes every stored name.</summary>
    public void Clear() => _vars.Clear();

    /// <summary>The stored values as a JSON object.</summary>
    public override string ToString()
    {
        var obj = new JsonObject();
        foreach (var (key, value) in _vars)
        {
            obj[key] = value?.DeepClone();
        }

        return obj.ToJsonString();
    }
}
