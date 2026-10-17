// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/05_progressive_disclosure.py

namespace Nooa.Examples.Quickstart.Example05;

/// <summary>Agent that appraises items without knowing their types ahead of time.</summary>
public partial class WarehouseAppraiser(AgentOptions? options = null) : Agent(options)
{
    private readonly Dictionary<string, object> _warehouse = new()
    {
        ["ART-001"] = new Artwork("Starry Night Print", "Van Gogh Studio", 15000.0),
        ["STK-001"] = new StockHolding("NVDA", 100, 875.50),
        ["JWL-001"] = new Jewelry("Diamond Ring", 2.5, 8000.0),
        ["COL-001"] = new Collectible("Vintage Baseball Card", 5000.0, "excellent"),
        ["ART-002"] = new Artwork("Modern Abstract", "Local Artist", 2500.0),
        ["STK-002"] = new StockHolding("AAPL", 50, 185.25),
    };

    /// <summary>Retrieve an item from the warehouse by ID.</summary>
    public object? GetItem(string itemId) => _warehouse.GetValueOrDefault(itemId);

    /// <summary>Get the monetary value of an item.</summary>
    [Agentic]
    public partial Task<double> AppraiseItemAsync(string itemId);
}
