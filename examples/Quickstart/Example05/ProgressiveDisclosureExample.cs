// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/05_progressive_disclosure.py

using System.Globalization;

namespace Nooa.Examples.Quickstart.Example05;

/// <summary>Quickstart 05: the model calls Doc() to learn the type of an unknown object, then uses it.</summary>
public sealed class ProgressiveDisclosureExample : IQuickstartExample
{
    private static readonly string[] _itemIds = ["ART-001", "STK-001", "JWL-001", "COL-001"];

    public int Number => 5;

    public string Title => "Progressive disclosure";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var appraiser = new WarehouseAppraiser(new AgentOptions { Llm = llm });
        foreach (var itemId in _itemIds)
        {
            var value = await appraiser.AppraiseItemAsync(itemId);
            var typeName = appraiser.GetItem(itemId)?.GetType().Name;
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"  {itemId} ({typeName}): ${value:N2}"));
        }
    }
}
