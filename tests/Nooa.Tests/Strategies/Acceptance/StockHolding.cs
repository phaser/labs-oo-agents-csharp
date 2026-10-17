// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py

namespace Nooa.Tests.Strategies.Acceptance;

/// <summary>A stock position with shares and price.</summary>
public sealed class StockHolding(string symbol, int shares, double pricePerShare)
{
    /// <summary>The ticker symbol.</summary>
    public string Symbol { get; } = symbol;

    /// <summary>Calculate total value of the holding.</summary>
    public double GetTotalValue() => shares * pricePerShare;
}
