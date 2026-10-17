// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_reasoning_levels.py

using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ReasoningConfigTests
{
    private static Dictionary<string, object?> Obj(params (string Key, object? Value)[] items) =>
        items.ToDictionary(i => i.Key, i => i.Value);

    private static Dictionary<string, IReadOnlyDictionary<string, object?>> Levels() => new()
    {
        ["low"] = Obj(("reasoning", Obj(("effort", "low"), ("context", "all_turns")))),
        ["high"] = Obj(("reasoning", Obj(("effort", "high"), ("context", "all_turns")))),
    };

    private static ReasoningConfig Config(Dictionary<string, IReadOnlyDictionary<string, object?>>? levels = null, string? defaultLevel = null) =>
        new() { Levels = levels ?? Levels(), Default = defaultLevel };

    [Fact]
    public void Settings_UnknownLevels_SayUnknown()
    {
        var e = Assert.Throws<ArgumentException>(() => new ReasoningConfig().Settings("max"));

        Assert.Contains("unknown", e.Message);
    }

    [Fact]
    public void Settings_EmptyLevels_SayNotSupported()
    {
        var config = new ReasoningConfig { Levels = new Dictionary<string, IReadOnlyDictionary<string, object?>>() };

        var e = Assert.Throws<ArgumentException>(() => config.Settings("max"));

        Assert.Contains("not supported", e.Message);
    }

    [Fact]
    public void Settings_InvalidLevel_ListsAllowed()
    {
        var e = Assert.Throws<ArgumentException>(() => Config().Settings("max"));

        Assert.Contains("allowed: low, high", e.Message);
    }

    [Fact]
    public void Validate_EmptyLevelName_Throws()
    {
        var levels = new Dictionary<string, IReadOnlyDictionary<string, object?>> { [""] = Obj(("reasoning_effort", "low")) };

        Assert.Throws<ConfigValidationException>(() => Config(levels).Validate());
    }

    [Fact]
    public void Validate_EmptySettings_Throws()
    {
        var levels = new Dictionary<string, IReadOnlyDictionary<string, object?>> { ["low"] = Obj() };

        Assert.Throws<ConfigValidationException>(() => Config(levels).Validate());
    }

    [Fact]
    public void Validate_DefaultNotDeclared_Throws()
    {
        var levels = new Dictionary<string, IReadOnlyDictionary<string, object?>> { ["low"] = Obj(("reasoning_effort", "low")) };

        Assert.Throws<ConfigValidationException>(() => Config(levels, "high").Validate());
    }

    [Fact]
    public void Validate_DefaultWithoutLevels_Throws()
    {
        Assert.Throws<ConfigValidationException>(() => new ReasoningConfig { Default = "high" }.Validate());
    }

    [Theory]
    [InlineData("model")]
    [InlineData("api_base")]
    [InlineData("base_url")]
    [InlineData("api_key")]
    [InlineData("custom_llm_provider")]
    [InlineData("messages")]
    [InlineData("input")]
    [InlineData("extra_body")]
    [InlineData("reasoning_levels")]
    [InlineData("reasoning_default")]
    [InlineData("reasoning_level")]
    [InlineData("client")]
    public void Validate_ReservedField_Throws(string field)
    {
        var levels = new Dictionary<string, IReadOnlyDictionary<string, object?>> { ["low"] = Obj((field, "value")) };

        var e = Assert.Throws<ConfigValidationException>(() => Config(levels).Validate());

        Assert.Contains("reserved", e.Message);
        Assert.Contains(field, e.Message);
    }

    [Fact]
    public void Settings_NewProviderField_IsAllowedWithoutAllowlist()
    {
        var settings = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["low"] = Obj(("future_provider_control", Obj(("budget", 12)))),
        };

        var result = Config(settings).Settings("low");

        Assert.Equal(12, ((Dictionary<string, object?>)result["future_provider_control"]!)["budget"]);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("api_base")]
    [InlineData("extra_body")]
    [InlineData("reasoning_level")]
    public void Settings_ReservedFieldAddedAfterConstruction_Throws(string field)
    {
        var levels = new Dictionary<string, IReadOnlyDictionary<string, object?>> { ["low"] = Obj(("reasoning_effort", "low")) };
        var config = Config(levels);
        ((Dictionary<string, object?>)levels["low"])[field] = "injected";

        var e = Assert.Throws<ConfigValidationException>(() => config.Settings("low"));

        Assert.Contains(field, e.Message);
    }

    [Fact]
    public void Apply_NoSelection_KeepsRawControls()
    {
        var raw = Obj(("reasoning", Obj(("effort", "medium"), ("summary", "auto"))));

        var result = Config(defaultLevel: "low").Apply(raw, null);

        Assert.Equal(raw, result);
    }

    [Fact]
    public void Apply_Selection_ReplacesWholeBlocksAndDoesNotMutateInputs()
    {
        var raw = Obj(("reasoning", Obj(("effort", "high"), ("summary", "auto"))));
        var config = Config();

        var first = config.Apply(raw, "low");
        ((Dictionary<string, object?>)first["reasoning"]!)["effort"] = "changed by caller";
        var second = config.Apply(raw, "low");

        // No implicit merge of "summary" from the defaults.
        var reasoning = Assert.IsType<Dictionary<string, object?>>(second["reasoning"]);
        Assert.Equal(["context", "effort"], reasoning.Keys.Order());
        Assert.Equal("low", reasoning["effort"]);
        Assert.Equal("auto", ((Dictionary<string, object?>)raw["reasoning"]!)["summary"]);
    }

    [Fact]
    public void Apply_Selection_ReplacesInheritedExtraBodyEntryWithoutMutatingIt()
    {
        var inherited = Obj(("reasoning", Obj(("effort", "high"))), ("other", Obj(("enabled", true))));
        var defaults = Obj(("extra_body", inherited));

        var result = Config().Apply(defaults, "low");

        var extra = Assert.IsType<Dictionary<string, object?>>(result["extra_body"]);
        Assert.Equal(["other"], extra.Keys);
        Assert.True(inherited.ContainsKey("reasoning"));
    }

    [Fact]
    public void Apply_InvalidSelection_Throws()
    {
        Assert.Throws<ArgumentException>(() => Config().Apply(Obj(), "max"));
    }

    [Fact]
    public void Apply_ReplyCapInLevel_ReplacesAllInheritedSpellings()
    {
        var levels = new Dictionary<string, IReadOnlyDictionary<string, object?>> { ["short"] = Obj(("max_completion_tokens", 100)) };
        var defaults = Obj(("max_tokens", 5000), ("extra_body", Obj(("max_output_tokens", 7000), ("keep", 1))));

        var result = Config(levels).Apply(defaults, "short");

        Assert.Equal(100, result["max_completion_tokens"]);
        Assert.False(result.ContainsKey("max_tokens"));
        Assert.Equal(["keep"], ((Dictionary<string, object?>)result["extra_body"]!).Keys);
    }

    [Fact]
    public void Apply_ReplyCapInExtraBody_MovesToTopLevel()
    {
        var defaults = Obj(("extra_body", Obj(("max_tokens", 300), ("keep", 1))));

        var result = new ReasoningConfig().Apply(defaults, null);

        Assert.Equal(300, result["max_tokens"]);
        Assert.Equal(["keep"], ((Dictionary<string, object?>)result["extra_body"]!).Keys);
    }

    [Fact]
    public void Apply_TwoReplyCaps_Throws()
    {
        var defaults = Obj(("max_tokens", 1), ("max_output_tokens", 2));

        Assert.Throws<ArgumentException>(() => new ReasoningConfig().Apply(defaults, null));
    }
}
