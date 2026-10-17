// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/reasoning.py

using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>
/// Maps public level names to request settings for one configured route.
/// </summary>
/// <remarks>
/// <para>
/// Null <see cref="Levels"/> means unknown support. An empty mapping means unsupported.
/// <see cref="Default"/> documents the default of the route. It is not a request to send it on every call.
/// Declarations live in registry YAML, not in a model-name table.
/// </para>
/// <para>
/// Per-call route overrides (<c>api_base</c>, <c>model</c>, <c>client</c>) and per-call request settings do not exist
/// in the C# client, so the upstream checks for them have no counterpart here.
/// </para>
/// </remarks>
public sealed record ReasoningConfig
{
    private const string _extraBody = "extra_body";

    // Settings that select the client or the request itself, not the effort behavior of a provider.
    private static readonly HashSet<string> _reserved =
    [
        "reasoning_levels", "reasoning_default", "reasoning_level", "model", "api_base", "base_url", "api_key",
        "custom_llm_provider", "messages", "input", _extraBody, "client",
    ];

    /// <summary>The declared levels: level name to request settings. Null when unknown.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Levels { get; init; }

    /// <summary>The level that the route uses by default, as documentation. Null for none.</summary>
    public string? Default { get; init; }

    /// <summary>Rejects malformed levels and request-control fields.</summary>
    /// <exception cref="ConfigValidationException">A level name is empty, a level has no settings or a reserved field, or the default is not declared.</exception>
    public void Validate()
    {
        if (Default is not null && !(Levels?.ContainsKey(Default) ?? false))
        {
            throw new ConfigValidationException("reasoning_default must name a declared reasoning level");
        }

        foreach (var (level, settings) in Levels ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
        {
            if (string.IsNullOrWhiteSpace(level) || settings.Count == 0)
            {
                throw new ConfigValidationException("reasoning_levels must have non-empty names and request settings");
            }

            var conflict = settings.Keys.Where(_reserved.Contains).Order(StringComparer.Ordinal).ToList();
            if (conflict.Count > 0)
            {
                throw new ConfigValidationException(
                    $"reasoning level '{level}' contains reserved fields: {string.Join(", ", conflict)}");
            }
        }
    }

    /// <summary>Validates a selection and returns a detached copy of its settings.</summary>
    /// <exception cref="ArgumentException">The levels are unknown or unsupported, or the level is not declared.</exception>
    public IReadOnlyDictionary<string, object?> Settings(string level)
    {
        if (Levels is null)
        {
            throw new ArgumentException("Reasoning levels are unknown for this route; declare reasoning_levels", nameof(level));
        }

        if (Levels.Count == 0)
        {
            throw new ArgumentException("Reasoning-level selection is not supported for this route", nameof(level));
        }

        if (!Levels.TryGetValue(level, out var settings))
        {
            throw new ArgumentException($"Invalid reasoning level '{level}'; allowed: {string.Join(", ", Levels.Keys)}", nameof(level));
        }

        // Re-check the declaration: the nested dictionaries are not frozen, so a later edit must not add a routing control.
        Validate();
        return (Dictionary<string, object?>)DeepCopy(settings)!;
    }

    /// <summary>
    /// Resolves the effort once, before the client sends a request.
    /// </summary>
    /// <remarks>
    /// No selection leaves the defaults untouched. A selected level replaces whole top-level values of the defaults.
    /// There is no provider-specific merge. A reply cap in the level replaces every inherited spelling of the cap,
    /// also inside <c>extra_body</c>. A reply cap in <c>extra_body</c> moves to the top level. This changes the
    /// requested effort, never stored reasoning or replay compatibility.
    /// </remarks>
    /// <param name="defaults">The default request settings of the alias.</param>
    /// <param name="selection">The level to apply, or null for none.</param>
    /// <exception cref="ArgumentException">The level is not valid, or two reply caps are set.</exception>
    public IReadOnlyDictionary<string, object?> Apply(IReadOnlyDictionary<string, object?> defaults, string? selection)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        var result = defaults.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        if (selection is null)
        {
            return PromoteReplyCap(result);
        }

        var patch = Settings(selection);
        ReplaceReplyCap(result, patch);
        if (result.TryGetValue(_extraBody, out var inherited) && inherited is IReadOnlyDictionary<string, object?> extra
            && patch.Keys.Any(extra.ContainsKey))
        {
            result[_extraBody] = extra.Where(kv => !patch.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }

        foreach (var (key, value) in patch)
        {
            result[key] = value;
        }

        return PromoteReplyCap(result);
    }

    // A cap in the patch replaces all inherited spellings, also the ones in extra_body.
    private static void ReplaceReplyCap(Dictionary<string, object?> parameters, IReadOnlyDictionary<string, object?> patch)
    {
        IReadOnlyDictionary<string, object?> supplied = patch;
        if (patch.TryGetValue(_extraBody, out var patchExtra) && patchExtra is IReadOnlyDictionary<string, object?> nested)
        {
            supplied = patch.Concat(nested).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Last().Value);
        }

        var keys = ContextLimits.ReplyCapKeys.Where(supplied.ContainsKey).ToList();
        if (keys.Count == 0)
        {
            return;
        }

        if (keys.Count > 1)
        {
            throw new ArgumentException("Use only one reply token limit field per configuration layer");
        }

        foreach (var key in ContextLimits.ReplyCapKeys)
        {
            parameters.Remove(key);
        }

        if (parameters.TryGetValue(_extraBody, out var inherited) && inherited is IReadOnlyDictionary<string, object?> extra)
        {
            parameters[_extraBody] = WithoutReplyCap(extra);
        }
    }

    // Reply caps are standard request settings. Some transports ignore them inside extra_body.
    private static Dictionary<string, object?> PromoteReplyCap(Dictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue(_extraBody, out var value) && value is IReadOnlyDictionary<string, object?> extra
            && ContextLimits.ReplyCapKeys.Any(extra.ContainsKey))
        {
            foreach (var key in ContextLimits.ReplyCapKeys.Where(extra.ContainsKey))
            {
                parameters[key] = extra[key];
            }

            parameters[_extraBody] = WithoutReplyCap(extra);
        }

        if (ContextLimits.ReplyCapKeys.Count(parameters.ContainsKey) > 1)
        {
            throw new ArgumentException("Use only one reply token limit field");
        }

        return parameters;
    }

    private static Dictionary<string, object?> WithoutReplyCap(IReadOnlyDictionary<string, object?> extra) =>
        extra.Where(kv => !ContextLimits.ReplyCapKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    private static object? DeepCopy(object? value) => value switch
    {
        IReadOnlyDictionary<string, object?> map => map.ToDictionary(kv => kv.Key, kv => DeepCopy(kv.Value), StringComparer.Ordinal),
        List<object?> list => list.Select(DeepCopy).ToList(),
        _ => value,
    };
}
