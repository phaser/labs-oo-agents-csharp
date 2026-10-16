// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/truncation_config.py

namespace Nooa.Config;

/// <summary>
/// Controls output size at render time.
/// </summary>
/// <remarks>
/// <para>There are two layers of bounds.</para>
/// <list type="number">
/// <item>A whole-context token budget (<see cref="MaxContextTokens"/>), applied when the context is assembled.</item>
/// <item>
/// Sub-configs by mechanism: <see cref="Capture"/> (head and tail truncation of raw text streams),
/// <see cref="MediaCapture"/> (cap on media blocks per cell), <see cref="EventFormat"/>
/// (rendering of event fields on every turn, so generous), <see cref="PrefillFormat"/>
/// (rendering of parameters at method invocation, one time per call, so tighter) and
/// <see cref="ContextBlockFormat"/> (values that the agent author places in the context, unlimited by default).
/// </item>
/// </list>
/// <para>
/// <see cref="MaxEventTokens"/> and <see cref="MinPreservedEvents"/> are kept for configuration compatibility.
/// They are not enforced now, because events are not evicted separately. Use <see cref="MaxContextTokens"/>
/// for an enforced budget.
/// </para>
/// </remarks>
public sealed record TruncationConfig : ConfigBase
{
    private static readonly string[] _subConfigNames =
    [
        nameof(Capture),
        nameof(MediaCapture),
        nameof(EventFormat),
        nameof(PrefillFormat),
        nameof(ContextBlockFormat),
    ];

    /// <summary>The default configuration.</summary>
    public static TruncationConfig Default { get; } = new();

    /// <summary>Total context token budget. Null means no budget.</summary>
    public int? MaxContextTokens { get; init { field = value; MarkSet(); } }

    /// <summary>
    /// Reserved for compatibility. Not enforced now, because events are not evicted separately.
    /// Use <see cref="MaxContextTokens"/> for an enforced budget.
    /// </summary>
    public int? MaxEventTokens { get; init { field = value; MarkSet(); } }

    /// <summary>
    /// Reserved for compatibility. Not enforced now, because events are not evicted separately.
    /// Use <see cref="MaxContextTokens"/> for an enforced budget.
    /// </summary>
    public int MinPreservedEvents { get; init { field = value; MarkSet(); } } = 5;

    /// <summary>
    /// Planning reserve when no effective reply cap is configured. It shrinks the usable context window
    /// for budgeting and utilization. 0 disables the fallback, not an explicit reply cap.
    /// </summary>
    public int ResponseReserveTokens { get; init { field = value; MarkSet(); } } = 4_096;

    /// <summary>Budgets for captured text streams.</summary>
    public CaptureConfig Capture { get; init { field = value; MarkSet(); } } = new();

    /// <summary>Cap on captured media blocks.</summary>
    public MediaCaptureConfig MediaCapture { get; init { field = value; MarkSet(); } } = new();

    /// <summary>Generous bounds: rendered every turn, and the model must read what its code returned.</summary>
    public FormatConfig EventFormat { get; init { field = value; MarkSet(); } } = new() { MaxString = 10_000, MaxLength = 200, MaxDepth = 5 };

    /// <summary>Bounds for one-time rendering per method call. The agent author controls the inputs.</summary>
    public FormatConfig PrefillFormat { get; init { field = value; MarkSet(); } } = new() { MaxString = 2_000, MaxLength = 25, MaxDepth = 4 };

    /// <summary>
    /// Unlimited bounds for values that the agent author places in the context. Whole-block eviction
    /// handles overflow when the context is assembled.
    /// </summary>
    public FormatConfig ContextBlockFormat { get; init { field = value; MarkSet(); } } = new() { MaxString = null, MaxLength = null, MaxDepth = null };

    /// <summary>
    /// Validates the sub-configs and then the token budgets.
    /// </summary>
    public override void Validate()
    {
        Capture.Validate();
        MediaCapture.Validate();
        EventFormat.Validate();
        PrefillFormat.Validate();
        ContextBlockFormat.Validate();

        var errors = new List<string>();
        var budgets = new (string Name, int? Value)[]
        {
            (nameof(MaxContextTokens), MaxContextTokens),
            (nameof(MaxEventTokens), MaxEventTokens),
        };
        foreach (var (name, value) in budgets)
        {
            if (value is <= 0)
            {
                errors.Add($"{name} must be > 0 or null, got {value}");
            }
        }

        ThrowIfInvalid(nameof(TruncationConfig), errors);
    }

    /// <summary>
    /// Merges with another config. The other config takes precedence for the fields it sets explicitly.
    /// </summary>
    /// <remarks>
    /// Sub-configs merge field by field: an <paramref name="other"/> sub-config that sets only one field
    /// does not replace the other fields of the base sub-config. Top-level fields follow the same rule.
    /// </remarks>
    /// <returns>This instance when <paramref name="other"/> is null, otherwise a new instance.</returns>
    public TruncationConfig MergeWith(TruncationConfig? other)
    {
        if (other is null)
        {
            return this;
        }

        RequireFieldsSet(other);
        var merged = CopySetFields(this with { }, other, _subConfigNames);
        if (other.IsSet(nameof(Capture)))
        {
            merged = merged with { Capture = CopySetFields(Capture with { }, other.Capture) };
        }

        if (other.IsSet(nameof(MediaCapture)))
        {
            merged = merged with { MediaCapture = CopySetFields(MediaCapture with { }, other.MediaCapture) };
        }

        if (other.IsSet(nameof(EventFormat)))
        {
            merged = merged with { EventFormat = CopySetFields(EventFormat with { }, other.EventFormat) };
        }

        if (other.IsSet(nameof(PrefillFormat)))
        {
            merged = merged with { PrefillFormat = CopySetFields(PrefillFormat with { }, other.PrefillFormat) };
        }

        if (other.IsSet(nameof(ContextBlockFormat)))
        {
            merged = merged with { ContextBlockFormat = CopySetFields(ContextBlockFormat with { }, other.ContextBlockFormat) };
        }

        return merged;
    }
}
