// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/truncation_config.py

namespace Nooa.Config;

/// <summary>
/// Recursive bounds for object rendering (<c>PFormat</c>).
/// </summary>
/// <remarks>
/// Used at several rendering moments, see <see cref="TruncationConfig.EventFormat"/> and
/// <see cref="TruncationConfig.PrefillFormat"/>. A per-field attribute can override these defaults on one field.
/// A null limit means unlimited.
/// </remarks>
public sealed record FormatConfig : ConfigBase
{
    /// <summary>Maximum characters per string before a length marker replaces the middle.</summary>
    public int? MaxString { get; init { field = value; MarkSet(); } } = 500;

    /// <summary>Maximum items per container before a length marker replaces the middle.</summary>
    public int? MaxLength { get; init { field = value; MarkSet(); } } = 50;

    /// <summary>Maximum nesting depth.</summary>
    public int? MaxDepth { get; init { field = value; MarkSet(); } } = 4;

    public override void Validate()
    {
        var errors = new List<string>();
        var limits = new (string Name, int? Value)[]
        {
            (nameof(MaxString), MaxString),
            (nameof(MaxLength), MaxLength),
            (nameof(MaxDepth), MaxDepth),
        };
        foreach (var (name, value) in limits)
        {
            if (value is <= 0)
            {
                errors.Add($"{name} must be > 0 or null, got {value}");
            }
        }

        ThrowIfInvalid(nameof(FormatConfig), errors);
    }
}
