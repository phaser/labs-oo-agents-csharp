// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (_jsonable) tests (intent)

using System.Text.Json.Nodes;
using Nooa.Config;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

public class CellValuesTests
{
    private static readonly FormatConfig _format = new() { MaxString = 100, MaxLength = 3, MaxDepth = 3 };

    [Fact]
    public void ToJson_Null_IsNull()
    {
        Assert.Null(CellValues.ToJson(null, _format));
    }

    [Fact]
    public void ToJson_Scalars_KeepTheirJsonForm()
    {
        Assert.Equal(5, CellValues.ToJson(5, _format)!.GetValue<int>());
        Assert.Equal("s", CellValues.ToJson("s", _format)!.GetValue<string>());
        Assert.True(CellValues.ToJson(true, _format)!.GetValue<bool>());
    }

    [Fact]
    public void ToJson_Record_UsesTheSerializerNames()
    {
        var node = Assert.IsType<JsonObject>(CellValues.ToJson(new OrderResult(true, 1.5, ["a"]), _format));

        Assert.Equal(1.5, node["total_cost"]!.GetValue<double>());
    }

    [Fact]
    public void ToJson_JsonNode_IsCopied()
    {
        var source = new JsonObject { ["a"] = 1 };

        var copy = CellValues.ToJson(source, _format);

        Assert.NotSame(source, copy);
        Assert.Equal("{\"a\":1}", copy!.ToJsonString());
    }

    [Fact]
    public void ToJson_Collection_BecomesBoundedText()
    {
        var node = CellValues.ToJson(Enumerable.Range(0, int.MaxValue), _format);

        Assert.True(node is JsonValue v && v.TryGetValue<string>(out var text) && text.Length < 300);
    }

    [Fact]
    public void ToJson_ValueWithNoJsonForm_BecomesText()
    {
        var node = CellValues.ToJson(new Func<int>(() => 1), _format);

        Assert.True(node is JsonValue v && v.TryGetValue<string>(out _));
    }
}
