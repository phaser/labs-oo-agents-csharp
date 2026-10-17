// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py

namespace Nooa.Examples.Quickstart.Example05;

/// <summary>A piece of jewelry valued by carats.</summary>
public sealed class Jewelry(string description, double carats, double ratePerCarat)
{
    /// <summary>The description of the piece.</summary>
    public string Description { get; } = description;

    /// <summary>Compute total value based on carats and rate.</summary>
    public double ComputeValue() => carats * ratePerCarat;
}
