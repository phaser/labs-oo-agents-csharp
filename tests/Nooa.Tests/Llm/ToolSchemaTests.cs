// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_tool_schema_clean.py, tests/unifiedllm/test_strict_schema_review_fixes.py, tests/unifiedllm/test_response_format_strict_fallback.py

using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ToolSchemaTests
{
    private sealed record Inner(string Name, int Value);

    private sealed record Optional(string Name, int? Age = null);

    private sealed record Wrapper(Inner Result);

    private sealed record Holder(Dictionary<string, Inner> Items);

    private sealed record Entry(string Text, double Score);

    private sealed record Config(string Host, int Port);

    private sealed record ServerResult([property: Description("Server configuration")] Config Config);

    private sealed record Node(string Name, Node? Child);

    private enum Color
    {
        Red,
        Green,
    }

    private static class Tools
    {
        public static string Add(int a, int b) => (a + b).ToString();

        public static string Search([Description("The query")] string query, int limit = 5, CancellationToken cancellationToken = default) =>
            query + limit;
    }

    private static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static JsonObject Clean(Type type, bool strict = false) => ToolSchema.Clean(ToolSchema.FromType(type), strict);

    [Fact]
    public void FromMethod_Parameters_BecomeRequiredProperties()
    {
        var schema = ToolSchema.Clean(ToolSchema.FromMethod(typeof(Tools).GetMethod(nameof(Tools.Add))!));

        Assert.Equal("object", schema["type"]!.GetValue<string>());
        Assert.Equal("integer", schema["properties"]!["a"]!["type"]!.GetValue<string>());
        Assert.Equal(["a", "b"], schema["required"]!.AsArray().Select(r => r!.GetValue<string>()));
        Assert.DoesNotContain("title", schema.ToJsonString());
    }

    [Fact]
    public void FromMethod_DefaultParameterAndCancellationToken_OptionalAndSkipped()
    {
        var schema = ToolSchema.Clean(ToolSchema.FromMethod(typeof(Tools).GetMethod(nameof(Tools.Search))!));

        Assert.Equal(["query"], schema["required"]!.AsArray().Select(r => r!.GetValue<string>()));
        Assert.Equal(["query", "limit"], schema["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal("The query", schema["properties"]!["query"]!["description"]!.GetValue<string>());
    }

    [Fact]
    public void FromMethod_NullMethod_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ToolSchema.FromMethod((MethodBase)null!));
    }

    [Fact]
    public void Clean_NestedRecord_InlinesWithoutRefsOrTitles()
    {
        var schema = Clean(typeof(Wrapper));
        var serialized = schema.ToJsonString();

        Assert.DoesNotContain("$ref", serialized);
        Assert.DoesNotContain("$defs", serialized);
        Assert.DoesNotContain("title", serialized);
        var result = schema["properties"]!["result"]!;
        Assert.Equal("object", result["type"]!.GetValue<string>());
        Assert.Equal("string", result["properties"]!["name"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Clean_DictionaryOfRecords_InlinesAdditionalProperties()
    {
        var schema = Clean(typeof(Holder));

        var items = schema["properties"]!["items"]!;
        Assert.Equal("object", items["additionalProperties"]!["type"]!.GetValue<string>());
        Assert.NotNull(items["additionalProperties"]!["properties"]!["name"]);
    }

    [Fact]
    public void Clean_ListOfRecords_InlinesItems()
    {
        var schema = Clean(typeof(List<Entry>));

        // A bare list has no object root: Clean keeps the shape guarantee, so the type becomes the list's own type.
        Assert.Equal("array", schema["type"]!.GetValue<string>());
    }

    [Fact]
    public void ResolveRefs_Definitions_AreInlinedAndRemoved()
    {
        var schema = Parse("""
            {"type":"object","properties":{"r":{"$ref":"#/$defs/Entry"}},
             "$defs":{"Entry":{"type":"object","properties":{"text":{"type":"string"}}}}}
            """);

        var resolved = ToolSchema.ResolveRefs(schema);

        Assert.Null(resolved["$defs"]);
        Assert.Equal("string", resolved["properties"]!["r"]!["properties"]!["text"]!["type"]!.GetValue<string>());
        Assert.NotNull(schema["$defs"]);
    }

    [Fact]
    public void ResolveRefs_SingleAllOf_IsUnwrappedKeepingSiblings()
    {
        var schema = Parse("""
            {"type":"object","properties":{"config":{"allOf":[{"$ref":"#/$defs/C"}],"description":"Server configuration"}},
             "$defs":{"C":{"type":"object","properties":{"host":{"type":"string"}}}}}
            """);

        var config = ToolSchema.ResolveRefs(schema)["properties"]!["config"]!;

        Assert.Null(config["allOf"]);
        Assert.Equal("Server configuration", config["description"]!.GetValue<string>());
        Assert.NotNull(config["properties"]!["host"]);
    }

    [Fact]
    public void ResolveRefs_RecursiveType_BreaksTheCycle()
    {
        var resolved = ToolSchema.ResolveRefs(ToolSchema.FromType(typeof(Node)));

        Assert.DoesNotContain("$ref", resolved.ToJsonString());
    }

    [Fact]
    public void ResolveRefs_UnresolvableRef_BecomesAnObject()
    {
        var schema = Parse("""{"$defs":{"A":{"type":"string"}},"properties":{"x":{"$ref":"https://example.test/x"}}}""");

        var resolved = ToolSchema.ResolveRefs(schema);

        Assert.Equal("object", resolved["properties"]!["x"]!["type"]!.GetValue<string>());
        Assert.Single(resolved["properties"]!["x"]!.AsObject());
    }

    [Fact]
    public void Clean_Strict_ForcesAllPropertiesRequiredOnNestedObjects()
    {
        var schema = Clean(typeof(Wrapper2), strict: true);

        var inner = schema["properties"]!["result"]!;
        Assert.Equal("object", inner["type"]!.GetValue<string>());
        Assert.Equal(
            inner["properties"]!.AsObject().Select(p => p.Key).Order(),
            inner["required"]!.AsArray().Select(r => r!.GetValue<string>()).Order());
        Assert.False(inner["additionalProperties"]!.GetValue<bool>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
    }

    private sealed record Wrapper2(Optional Result);

    [Fact]
    public void Clean_Strict_DropsKeywordsOutsideTheAllowlist()
    {
        var schema = Clean(typeof(Entry), strict: true);

        Assert.DoesNotContain("title", schema.ToJsonString());
        Assert.DoesNotContain("$schema", schema.ToJsonString());
    }

    [Fact]
    public void IsStrictValid_PartialRequired_IsRejected()
    {
        var schema = Parse("""
            {"type":"object","properties":{"result":{"type":"object",
             "properties":{"name":{"type":"string"},"age":{"type":"integer"}},
             "required":["name"],"additionalProperties":false}},
             "required":["result"],"additionalProperties":false}
            """);

        Assert.False(ToolSchema.IsStrictValid(schema));
    }

    [Fact]
    public void IsStrictValid_FullRequired_IsAccepted()
    {
        var schema = Parse("""
            {"type":"object","properties":{"result":{"type":"object",
             "properties":{"name":{"type":"string"},"age":{"type":"integer"}},
             "required":["name","age"],"additionalProperties":false}},
             "required":["result"],"additionalProperties":false}
            """);

        Assert.True(ToolSchema.IsStrictValid(schema));
    }

    [Fact]
    public void IsStrictValid_PropertyWithoutType_IsRejected()
    {
        Assert.False(ToolSchema.IsStrictValid(Parse("""{"type":"object","properties":{"result":{}},"required":["result"]}""")));
    }

    [Theory]
    [InlineData("""{"type":"object","properties":{"x":{"type":"array"}},"required":["x"]}""", false)]
    [InlineData("""{"type":"object","properties":{"x":{"type":"array","items":{}}},"required":["x"]}""", false)]
    [InlineData("""{"type":"object","properties":{"x":{"type":"array","items":{"type":"string"}}},"required":["x"]}""", true)]
    public void IsStrictValid_Arrays_NeedTypedItems(string json, bool expected)
    {
        Assert.Equal(expected, ToolSchema.IsStrictValid(Parse(json)));
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(List<Inner>))]
    [InlineData(typeof(Color))]
    [InlineData(typeof(Inner))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(Optional))]
    public void IsStrictCompatible_RegularTypes_AreCompatible(Type type)
    {
        Assert.True(ToolSchema.IsStrictCompatible(ToolSchema.ResolveRefs(ToolSchema.FromType(type))));
    }

    [Theory]
    [InlineData(typeof(Dictionary<string, int>))]
    [InlineData(typeof(Dictionary<string, object>))]
    [InlineData(typeof(object))]
    [InlineData(typeof(List<object>))]
    public void IsStrictCompatible_FreeFormTypes_AreNotCompatible(Type type)
    {
        Assert.False(ToolSchema.IsStrictCompatible(ToolSchema.ResolveRefs(ToolSchema.FromType(type))));
    }

    [Fact]
    public void IsStrictCompatible_ReportedShapes_FollowTheRules()
    {
        Assert.False(ToolSchema.IsStrictCompatible(Parse("""{"type":"object","additionalProperties":true}""")));
        Assert.False(ToolSchema.IsStrictCompatible(Parse("""{"type":"object"}""")));
        Assert.False(ToolSchema.IsStrictCompatible(Parse("""{"type":"array","prefixItems":[{"type":"integer"}],"items":{"type":"integer"}}""")));
        Assert.False(ToolSchema.IsStrictCompatible(Parse("""{"type":"array","uniqueItems":true,"items":{"type":"string"}}""")));
        Assert.False(ToolSchema.IsStrictCompatible(Parse("""{"type":"object","properties":{"x":{}}}""")));
        Assert.True(ToolSchema.IsStrictCompatible(Parse("""{"type":"object","properties":{"x":{"type":"string"}},"additionalProperties":false}""")));
    }

    [Fact]
    public void Loose_Schema_DropsUnsupportedKeywordsAndAddsDefaults()
    {
        var schema = Parse("""
            {"type":"object","title":"T","properties":{
              "t":{"type":"array","prefixItems":[{"type":"integer"}],"minItems":2,"maxItems":2},
              "d":{"type":"object","additionalProperties":{"type":"integer"}},
              "s":{"type":"string","pattern":"^a","format":"email"}}}
            """);

        var loose = ToolSchema.Loose(schema);

        var serialized = loose.ToJsonString();
        Assert.DoesNotContain("prefixItems", serialized);
        Assert.DoesNotContain("minItems", serialized);
        Assert.DoesNotContain("pattern", serialized);
        Assert.DoesNotContain("title", serialized);
        Assert.NotNull(loose["properties"]!["t"]!["items"]);
        Assert.NotNull(loose["properties"]!["d"]!["properties"]);
    }
}
