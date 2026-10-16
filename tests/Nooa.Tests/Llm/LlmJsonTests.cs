// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_json_parsing.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class LlmJsonTests
{
    private static string Answer(JsonNode? node) => node!["answer"]!.GetValue<string>();

    [Fact]
    public void ExtractAndParse_PlainJson_Parses()
    {
        var node = LlmJson.ExtractAndParse("{\"answer\": \"42\", \"confidence\": 0.9}");

        Assert.Equal("42", Answer(node));
        Assert.Equal(0.9, node!["confidence"]!.GetValue<double>());
    }

    [Theory]
    [InlineData("```json\n{\"answer\": \"42\"}\n```")]
    [InlineData("```\n{\"answer\": \"42\"}\n```")]
    [InlineData("**{\"answer\": \"42\"}**")]
    [InlineData("*{\"answer\": \"42\"}*")]
    [InlineData("** {\"answer\": \"42\"} **")]
    [InlineData("Here is the answer: {\"answer\": \"42\"} and more text")]
    public void ExtractAndParse_WrappedJson_Unwraps(string text)
    {
        Assert.Equal("42", Answer(LlmJson.ExtractAndParse(text)));
    }

    [Fact]
    public void ExtractAndParse_BashFenceBeforeJson_DoesNotMaskLaterJson()
    {
        const string text = "Run the example:\n\n```bash\nuv run python scripts/run_local.py\n```\n\nResult: {\"answer\": \"42\"}\n";

        Assert.Equal("42", Answer(LlmJson.ExtractAndParse(text)));
    }

    [Fact]
    public void ExtractAndParse_OnlyBashFence_ThrowsWithoutTheText()
    {
        const string text = "```bash\nuv run python scripts/run_local.py\n```";

        var error = Assert.Throws<JsonException>(() => LlmJson.ExtractAndParse(text));

        Assert.DoesNotContain("uv run", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not json at all")]
    public void ExtractAndParse_NoJson_Throws(string text)
    {
        Assert.Throws<JsonException>(() => LlmJson.ExtractAndParse(text));
    }

    [Theory]
    [InlineData("secret-model-text is not json")]
    [InlineData("   ")]
    [InlineData("{\"secret\": [...]}")]
    public void ExtractAndParse_Failure_MessageNeverContainsTheText(string text)
    {
        var error = Assert.Throws<JsonException>(() => LlmJson.ExtractAndParse(text));

        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public void ExtractAndParse_Ellipsis_AsksForCompleteJson()
    {
        var error = Assert.Throws<JsonException>(() => LlmJson.ExtractAndParse("{\"items\": [...]}"));

        Assert.Contains("unabbreviated", error.Message);
    }

    [Fact]
    public void ExtractAndParse_DoubleEncodedValue_IsDecoded()
    {
        var node = LlmJson.ExtractAndParse("{\"value\": \"{\\\"key\\\": \\\"val\\\"}\"}");

        Assert.Equal("val", node!["value"]!["key"]!.GetValue<string>());
    }

    [Fact]
    public void ExtractAndParse_BadEscape_IsRepaired()
    {
        var node = LlmJson.ExtractAndParse("{\"path\": \"C:\\Users\\x\"}");

        Assert.Equal("C:\\Users\\x", node!["path"]!.GetValue<string>());
    }

    [Fact]
    public void ExtractAndParse_ControlCharacter_IsRemoved()
    {
        var node = LlmJson.ExtractAndParse("{\"a\": \"x\u0001y\"}");

        Assert.Equal("xy", node!["a"]!.GetValue<string>());
    }

    [Fact]
    public void ExtractAndParse_LongWhitespaceRun_FailsAsJsonErrorWithoutHanging()
    {
        var text = "x" + new string(' ', 300_000) + "y";

        Assert.Throws<JsonException>(() => LlmJson.ExtractAndParse(text));
    }
}
