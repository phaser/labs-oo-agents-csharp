// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class JsonNodeValuesTests
{
    [Fact]
    public void ToPlain_Null_IsNull() => Assert.Null(JsonNodeValues.ToPlain(null));

    [Fact]
    public void ToPlain_Scalars_KeepTheirKind()
    {
        Assert.Equal("text", JsonNodeValues.ToPlain(JsonValue.Create("text")));
        Assert.Equal(5L, JsonNodeValues.ToPlain(JsonValue.Create(5)));
        Assert.Equal(2.5, JsonNodeValues.ToPlain(JsonValue.Create(2.5)));
        Assert.Equal(true, JsonNodeValues.ToPlain(JsonValue.Create(true)));
    }

    [Fact]
    public void ToPlain_ParsedNumbers_AreIntegersWhenTheyFit()
    {
        var node = JsonNode.Parse("[1, 2.5, 12345678901234]")!;

        Assert.Equal([1L, 2.5, 12345678901234L], (List<object?>)JsonNodeValues.ToPlain(node)!);
    }

    [Fact]
    public void ToPlain_ObjectAndArray_BecomeDictionaryAndList()
    {
        var node = JsonNode.Parse("{\"a\": [1, {\"b\": null}], \"c\": \"d\"}")!;

        var plain = Assert.IsType<Dictionary<string, object?>>(JsonNodeValues.ToPlain(node));

        Assert.Equal("d", plain["c"]);
        var list = Assert.IsType<List<object?>>(plain["a"]);
        Assert.Null(((Dictionary<string, object?>)list[1]!)["b"]);
    }

    [Fact]
    public void Format_JsonNode_PrintsLikePlainValues() =>
        Assert.Equal("{\"a\": [1, 2], \"b\": \"x\"}", PFormat.Format(JsonNode.Parse("{\"a\": [1, 2], \"b\": \"x\"}")));
}
