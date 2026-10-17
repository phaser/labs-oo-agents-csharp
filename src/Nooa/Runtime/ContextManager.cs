// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context_manager.py

using Nooa.ContextBlocks;
using Nooa.Errors;

namespace Nooa.Runtime;

/// <summary>
/// Store for the named context blocks of an agent. Rendering the blocks into messages is not done here.
/// </summary>
/// <remarks>
/// <para>
/// Protected blocks (<c>system_prompt</c>, <c>self</c>, <c>state</c>) are registered with the
/// <c>Set*Protected</c> methods. The public setters, <see cref="Remove"/> and <see cref="Pop"/> refuse to change them
/// and throw <see cref="ProtectedBlockException"/>. A protected block can still be disabled.
/// </para>
/// <para>
/// A block with a provider keeps its resolved value in a cache. The runtime fills the cache with
/// <see cref="UpdateResolved"/> each turn. A setter or a removal clears the cache entry.
/// </para>
/// </remarks>
public sealed class ContextManager
{
    /// <summary>Key of the protected system prompt block.</summary>
    public const string SystemPromptKey = "system_prompt";

    /// <summary>Key of the protected block that documents the agent type.</summary>
    public const string SelfKey = "self";

    /// <summary>Key of the protected block that shows the current field values.</summary>
    public const string StateKey = "state";

    private readonly System.Collections.Generic.OrderedDictionary<string, ContextBlockEntry> _blocks = [];
    private readonly HashSet<string> _protected = [];
    private readonly HashSet<string> _disabled = [];
    private readonly Dictionary<string, object?> _resolved = [];

    /// <summary>Evaluates <see cref="Context.Expr"/> blocks. The agent sets it. Without it an expression block is refused.</summary>
    internal ContextExpressions? Expressions { get; set; }

    /// <summary>The keys of the protected blocks.</summary>
    public IReadOnlyCollection<string> ProtectedKeys => _protected;

    /// <summary>The number of blocks.</summary>
    public int Count => _blocks.Count;

    /// <summary>The block keys in insertion order.</summary>
    public IReadOnlyList<string> Keys => [.. _blocks.Keys];

    /// <summary>Returns true if a block exists.</summary>
    public bool Contains(string key) => _blocks.ContainsKey(key);

    /// <summary>Returns true if the block is in the static (cacheable) partition.</summary>
    public bool IsStatic(string key) => _blocks.TryGetValue(key, out var entry) && entry.IsStatic;

    /// <summary>Returns true if the key names a protected block.</summary>
    public bool IsProtected(string key) => _protected.Contains(key);

    /// <summary>
    /// Returns the value of a block. A block with a provider returns its last resolved value.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is no block with this key.</exception>
    /// <exception cref="DynamicNotResolvedException">The block has a provider that no turn has resolved yet.</exception>
    public object? Get(string key)
    {
        if (!_blocks.TryGetValue(key, out var entry))
        {
            throw new KeyNotFoundException(key);
        }

        if (!entry.IsDynamic)
        {
            return entry.Value;
        }

        return _resolved.TryGetValue(key, out var value) ? value : throw new DynamicNotResolvedException(key);
    }

    /// <summary>Returns a block value, or <paramref name="defaultValue"/> when the block does not exist.</summary>
    public object? GetOrDefault(string key, object? defaultValue = null)
    {
        try
        {
            return Get(key);
        }
        catch (KeyNotFoundException)
        {
            return defaultValue;
        }
    }

    /// <summary>Returns the stored blocks with their providers unresolved, in insertion order. The runtime renders from this.</summary>
    public IReadOnlyList<KeyValuePair<string, ContextBlockEntry>> RawItems() => [.. _blocks];

    /// <summary>
    /// Sets a block in the volatile partition. A null value disables the key and removes an unprotected block.
    /// A <see cref="Context"/> sets its own content (a value, a provider or an expression) and partition, and a <see cref="DynamicContext"/> sets a provider.
    /// </summary>
    /// <exception cref="ProtectedBlockException">The key is protected and the value is not null.</exception>
    public void Set(string key, object? value)
    {
        switch (value)
        {
            case null:
                Disable(key);
                if (!_protected.Contains(key) && _blocks.Remove(key))
                {
                    _resolved.Remove(key);
                }

                return;
            case Context { Expression: { } expression, Prefix: true }:
                SetStaticProvider(key, BindExpression(expression));
                return;
            case Context { Expression: { } expression }:
                SetDynamicProvider(key, BindExpression(expression));
                return;
            case Context { Provider: { } provider, Prefix: true }:
                SetStaticProvider(key, provider);
                return;
            case Context { Provider: { } provider }:
                SetDynamicProvider(key, provider);
                return;
            case Context { Prefix: true } context:
                SetStatic(key, context.Value!);
                return;
            case Context context:
                SetDynamic(key, context.Value);
                return;
            case DynamicContext dynamic:
                SetDynamicProvider(key, dynamic.Provider);
                return;
            default:
                SetDynamic(key, value);
                return;
        }
    }

    /// <summary>
    /// Applies one override of a block, for the overrides that the agent options set at construction. It works for
    /// a protected key too. The override keeps the partition of the block that it replaces. A new block goes to the
    /// volatile partition.
    /// </summary>
    /// <param name="key">The block key.</param>
    /// <param name="value">A plain value, a <see cref="Context"/>, a <see cref="DynamicContext"/>, or null to disable and remove the block.</param>
    public void ApplyOverride(string key, object? value)
    {
        var isProtected = _protected.Contains(key);
        switch (value)
        {
            case DynamicContext dynamic when isProtected:
                if (IsStatic(key))
                {
                    SetStaticProtected(key, dynamic.Provider);
                }
                else
                {
                    SetDynamicProtected(key, dynamic.Provider);
                }

                break;
            case not null and not Context and not DynamicContext when isProtected:
                SetProtectedValue(key, value);
                break;
            default:
                Set(key, value);
                break;
        }
    }

    /// <summary>Sets a plain block in the static (cacheable) partition.</summary>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void SetStatic(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Store(key, new ContextBlockEntry(value, null, true), refuseProtected: true);
    }

    /// <summary>Sets a block whose value the runtime computes each turn, in the static (cacheable) partition.</summary>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void SetStaticProvider(string key, Func<object?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Store(key, new ContextBlockEntry(null, provider, true), refuseProtected: true);
    }

    /// <summary>Sets a plain block in the volatile partition.</summary>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void SetDynamic(string key, object? value) =>
        Store(key, new ContextBlockEntry(value, null, false), refuseProtected: true);

    /// <summary>Sets a block whose value the runtime computes each turn, in the volatile partition.</summary>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void SetDynamicProvider(string key, Func<object?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Store(key, new ContextBlockEntry(null, provider, false), refuseProtected: true);
    }

    /// <summary>Registers a protected block with a provider in the static partition. Only the framework calls this.</summary>
    public void SetStaticProtected(string key, Func<object?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Store(key, new ContextBlockEntry(null, provider, true), refuseProtected: false);
        _protected.Add(key);
    }

    /// <summary>Registers a protected block with a provider in the volatile partition. Only the framework calls this.</summary>
    public void SetDynamicProtected(string key, Func<object?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Store(key, new ContextBlockEntry(null, provider, false), refuseProtected: false);
        _protected.Add(key);
    }

    /// <summary>Replaces the value of a protected block and keeps its partition. Only the framework calls this.</summary>
    public void SetProtectedValue(string key, object? value)
    {
        var isStatic = _blocks.TryGetValue(key, out var existing) && existing.IsStatic;
        Store(key, new ContextBlockEntry(value, null, isStatic), refuseProtected: false);
        _protected.Add(key);
    }

    /// <summary>Removes a block.</summary>
    /// <exception cref="KeyNotFoundException">There is no block with this key.</exception>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void Remove(string key)
    {
        if (!_blocks.ContainsKey(key))
        {
            throw new KeyNotFoundException(key);
        }

        if (_protected.Contains(key))
        {
            throw new ProtectedBlockException(key, "remove");
        }

        Delete(key);
    }

    /// <summary>
    /// Removes a block and returns its value (the last resolved value for a block with a provider).
    /// Returns <paramref name="defaultValue"/> when there is no such block.
    /// </summary>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public object? Pop(string key, object? defaultValue = null)
    {
        if (!_blocks.TryGetValue(key, out var entry))
        {
            return defaultValue;
        }

        if (_protected.Contains(key))
        {
            throw new ProtectedBlockException(key, "remove");
        }

        var value = entry.IsDynamic ? _resolved.GetValueOrDefault(key) : entry.Value;
        Delete(key);
        return value;
    }

    /// <summary>Removes a protected block. Only the framework calls this, for example for a class-level override of <c>null</c>.</summary>
    /// <exception cref="KeyNotFoundException">There is no block with this key.</exception>
    public void RemoveProtected(string key)
    {
        if (!_blocks.ContainsKey(key))
        {
            throw new KeyNotFoundException(key);
        }

        Delete(key);
        _protected.Remove(key);
    }

    /// <summary>Hides blocks from prompt construction without deleting them. A protected block can be disabled.</summary>
    public void Disable(params string[] keys)
    {
        foreach (var key in keys)
        {
            _disabled.Add(key);
            _resolved.Remove(key);
        }
    }

    /// <summary>Re-enables blocks that <see cref="Disable"/> hid.</summary>
    public void Enable(params string[] keys)
    {
        foreach (var key in keys)
        {
            _disabled.Remove(key);
        }
    }

    /// <summary>Returns true when the key is eligible for rendering.</summary>
    public bool IsEnabled(string key) => !_disabled.Contains(key);

    /// <summary>Returns true when the key is hidden.</summary>
    public bool IsDisabled(string key) => _disabled.Contains(key);

    /// <summary>Returns a copy of the hidden keys.</summary>
    public IReadOnlySet<string> Disabled() => new HashSet<string>(_disabled);

    /// <summary>Caches the resolved values of blocks with providers. The runtime calls this each turn.</summary>
    public void UpdateResolved(IReadOnlyDictionary<string, object?> resolved)
    {
        foreach (var (key, value) in resolved)
        {
            _resolved[key] = value;
        }
    }

    private Func<object?> BindExpression(string expression) =>
        (Expressions ?? throw new InvalidOperationException("This context manager has no agent, so it cannot evaluate an expression block."))
        .Bind(expression);

    private void Store(string key, ContextBlockEntry entry, bool refuseProtected)
    {
        if (refuseProtected && _protected.Contains(key))
        {
            throw new ProtectedBlockException(key);
        }

        _blocks[key] = entry;
        _disabled.Remove(key);
        _resolved.Remove(key);
    }

    private void Delete(string key)
    {
        _blocks.Remove(key);
        _disabled.Remove(key);
        _resolved.Remove(key);
    }
}
