// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context.py

using Nooa.ContextBlocks;
using Nooa.Errors;

namespace Nooa.Runtime;

/// <summary>
/// Dictionary-like API for managing what appears in your system prompt.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>self.Context</c> to add, update, or remove named blocks in your prompt. Static blocks are set once.
/// Dynamic blocks are evaluated again each LLM turn. Framework blocks (<c>system_prompt</c>, <c>self</c>, <c>state</c>)
/// are protected: you can hide them but you cannot change or remove them.
/// </para>
/// <code>
/// self.Context["notes"] = "Remember: user prefers JSON output";          // static text, volatile suffix
/// self.Context.Set("notes", value: "Keep it short", prefix: true);       // cacheable prefix
/// self.Context.Set("status", expr: "$\"{self.Done}/{self.Total} done\""); // C# expression, each turn
/// self.Context["notes"] = null;                                           // hide the block
/// var value = self.Context["notes"];                                      // KeyNotFoundException if missing
/// var maybe = self.Context.GetOrDefault("notes");                         // null if missing
/// self.Context.Remove("notes");                                           // delete
/// </code>
/// <para>
/// An expression is one C# expression with <c>self</c> bound to the agent. It must pass the same checks as a code cell
/// and it cannot call an LLM method. It is shown as text in the prompt. Load this library with <c>Doc(self.Context)</c>.
/// </para>
/// <para>
/// The API wraps the <see cref="ContextManager"/> of the agent. The state always lives there. An agent hides this API from
/// the model by default (<c>Agent.Context</c> is marked <see cref="HiddenAttribute"/>). A subclass opts in by declaring
/// <c>public new ContextApi Context =&gt; base.Context;</c>.
/// </para>
/// </remarks>
public sealed class ContextApi
{
    /// <summary>The longest block key in characters.</summary>
    public const int MaxKeyLength = 128;

    private static readonly string[] _strategyKeys = ["strategy_prompt", "execution_context"];

    private readonly ContextManager _manager;

    /// <summary>Creates the API for an agent.</summary>
    public ContextApi(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _manager = agent.ContextManager;
    }

    /// <summary>
    /// The value of a block. A protected block is readable by name. A block with an expression returns its last evaluated value.
    /// Assigning a string stores literal text in the volatile suffix. Assigning a <see cref="Context"/> controls content and placement.
    /// Assigning null hides the block.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Get: there is no block with this key.</exception>
    /// <exception cref="DynamicNotResolvedException">Get: the block has an expression that no turn has evaluated yet.</exception>
    /// <exception cref="ProtectedBlockException">Set: the key is protected and the value is not null.</exception>
    public object? this[string key]
    {
        get => _manager.Get(CheckKey(key));
        set => _manager.Set(CheckKey(key), value);
    }

    /// <summary>The number of blocks that are not protected.</summary>
    public int Count => Keys.Count;

    /// <summary>The keys of the blocks that are not protected, in insertion order.</summary>
    public IReadOnlyList<string> Keys => [.. _manager.Keys.Where(k => !_manager.IsProtected(k))];

    /// <summary>True when a block that is not protected has this key.</summary>
    public bool Contains(string key) => _manager.Contains(CheckKey(key)) && !_manager.IsProtected(key);

    /// <summary>The value of a block, or <paramref name="defaultValue"/> when there is none.</summary>
    public object? GetOrDefault(string key, object? defaultValue = null) => _manager.GetOrDefault(CheckKey(key), defaultValue);

    /// <summary>Deletes a block.</summary>
    /// <exception cref="KeyNotFoundException">There is no block with this key.</exception>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void Remove(string key) => _manager.Remove(CheckKey(key));

    /// <summary>Deletes a block and returns its value, or <paramref name="defaultValue"/> when there is none.</summary>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public object? Pop(string key, object? defaultValue = null) => _manager.Pop(CheckKey(key), defaultValue);

    /// <summary>
    /// Sets a block with named arguments. It is the same as assigning <c>Context.Of(value, prefix)</c> or <c>Context.Expr(expr, prefix)</c>.
    /// With neither value nor expression it hides the block.
    /// </summary>
    /// <param name="key">The block key.</param>
    /// <param name="value">Literal text. Not with <paramref name="expr"/>.</param>
    /// <param name="expr">A C# expression that is evaluated each LLM turn, for example <c>self.RenderNotes()</c>. Not with <paramref name="value"/>.</param>
    /// <param name="prefix">True places the block in the cacheable prefix, false in the volatile suffix.</param>
    /// <exception cref="ArgumentException">Both a value and an expression are given, or the expression is not accepted.</exception>
    /// <exception cref="RestrictedCodeException">The expression uses a restricted API.</exception>
    /// <exception cref="ProtectedBlockException">The key is protected.</exception>
    public void Set(string key, string? value = null, string? expr = null, bool prefix = false)
    {
        CheckKey(key);
        if (value is not null && expr is not null)
        {
            throw new ArgumentException("Set takes a value or an expression, not both.", nameof(expr));
        }

        _manager.Set(key, expr is not null ? Context.Expr(expr, prefix) : value is null ? null : prefix ? Context.Of(value, prefix: true) : value);
    }

    /// <summary>
    /// Sets a block with a C# expression in the volatile suffix. It is the same as <c>Set(key, expr: expression)</c>.
    /// </summary>
    /// <param name="key">The block key.</param>
    /// <param name="expression">One C# expression that is evaluated each LLM turn.</param>
    public void SetDynamic(string key, string expression) => _manager.Set(CheckKey(key), Context.Expr(expression));

    /// <summary>Hides blocks from future prompts without deleting them. A protected block can be hidden.</summary>
    public void Disable(params string[] keys) => _manager.Disable([.. keys.Select(CheckKey)]);

    /// <summary>Shows blocks again that <see cref="Disable"/> hid.</summary>
    public void Enable(params string[] keys) => _manager.Enable([.. keys.Select(CheckKey)]);

    /// <summary>The keys of the blocks that are hidden from prompts now.</summary>
    public IReadOnlySet<string> Disabled() => _manager.Disabled();

    /// <summary>True when a block key may appear in the prompt.</summary>
    public bool IsEnabled(string key) => _manager.IsEnabled(CheckKey(key));

    /// <summary>
    /// All block keys, protected ones included, and the well-known strategy keys (<c>strategy_prompt</c>, <c>execution_context</c>).
    /// Use it to find the keys that you can hide with <c>self.Context["key"] = null</c>.
    /// </summary>
    public IReadOnlySet<string> AllKeys() => new HashSet<string>(_manager.Keys.Concat(_strategyKeys));

    /// <inheritdoc />
    public override string ToString() => $"ContextApi({Count} blocks)";

    private static string CheckKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return key.Length <= MaxKeyLength ? key : throw new ArgumentException($"The key is longer than {MaxKeyLength} characters.", nameof(key));
    }
}
