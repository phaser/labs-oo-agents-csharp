// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py

namespace Nooa.Examples.Quickstart.Example05;

/// <summary>A collectible item whose value depends on condition.</summary>
public sealed class Collectible(string name, double baseValue, string condition)
{
    private static readonly Dictionary<string, double> _multipliers = new()
    {
        ["mint"] = 1.0,
        ["excellent"] = 0.85,
        ["good"] = 0.7,
        ["fair"] = 0.5,
    };

    /// <summary>The name of the item.</summary>
    public string Name { get; } = name;

    /// <summary>The condition: mint, excellent, good or fair.</summary>
    public string Condition { get; } = condition;

    /// <summary>Estimate value based on condition. Returns adjusted value.</summary>
    public double EstimateValue() => baseValue * _multipliers.GetValueOrDefault(Condition, 0.5);
}
