// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/03_codeact_tools.py

namespace Nooa.Examples.Quickstart.Example03;

/// <summary>You are an agent that checks inventory using deterministic helper methods.</summary>
public partial class InventoryAgent(AgentOptions? options = null) : Agent(options)
{
    // Private application state stays out of the model-visible state block.
    private readonly Dictionary<string, (int Stock, double Price)> _inventory = new()
    {
        ["apple"] = (50, 0.75),
        ["banana"] = (30, 0.50),
        ["orange"] = (0, 0.80),
    };

    /// <summary>Get current stock for an item.</summary>
    public int GetStock(string item) => _inventory.TryGetValue(item, out var entry) ? entry.Stock : 0;

    /// <summary>Get price for an item.</summary>
    public double GetPrice(string item) => _inventory.TryGetValue(item, out var entry) ? entry.Price : 0.0;

    /// <summary>Check if order can be fulfilled within budget.</summary>
    [Agentic]
    public partial Task<OrderResult> CanFulfillOrder(List<string> items, double budget);
}
