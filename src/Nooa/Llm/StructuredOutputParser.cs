// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Json;

namespace Nooa.Llm;

/// <summary>
/// Turns the text of a structured-output response into a typed value. The client and the Predict strategy share it.
/// </summary>
internal static class StructuredOutputParser
{
    /// <summary>The property that holds a value whose schema root is not an object.</summary>
    internal const string WrapperProperty = "result";

    /// <summary>
    /// True when the schema root is not an object. OpenAI needs an object root, so the value travels as
    /// <c>{"result": value}</c> and <see cref="Parse"/> unwraps it.
    /// </summary>
    internal static bool RequiresWrapper(JsonObject schema) =>
        schema["type"] is not JsonValue v || !v.TryGetValue<string>(out var rootType) || rootType != "object";

    /// <summary>Parses the response text into <paramref name="outputType"/>.</summary>
    /// <param name="response">The response. Reasoning stands in when the content is empty, as upstream does.</param>
    /// <param name="outputType">The requested type.</param>
    /// <param name="wrapped">True when the value sits under <see cref="WrapperProperty"/>.</param>
    /// <exception cref="StructuredOutputException">The text does not match the type.</exception>
    internal static object Parse(LlmResponse response, Type outputType, bool wrapped)
    {
        var text = response.Content.Length > 0 ? response.Content : response.Reasoning ?? "";
        JsonNode? node;
        try
        {
            node = LlmJson.ExtractAndParse(text);
        }
        catch (JsonException e)
        {
            // The messages of LlmJson hold no model text, so Detail may repeat them.
            throw new StructuredOutputException(outputType, e.Message, text, Location(e), e, isMalformedJson: true);
        }

        if (wrapped)
        {
            node = node is JsonObject obj && obj.TryGetPropertyValue(WrapperProperty, out var inner)
                ? inner
                : throw new StructuredOutputException(
                    outputType, $"the JSON object has no \"{WrapperProperty}\" property", text, "$");
        }

        try
        {
            return node.Deserialize(outputType, NooaJson.Options)
                ?? throw new StructuredOutputException(outputType, "the JSON value is null", text, "$");
        }
        catch (JsonException e)
        {
            // The message of System.Text.Json names the expected type and the path, not the value.
            throw new StructuredOutputException(
                outputType, $"a value does not match the type at {e.Path ?? "$"}", text, e.Path, e);
        }
        catch (NotSupportedException e)
        {
            throw new StructuredOutputException(outputType, "the type cannot be read from JSON", text, null, e);
        }
    }

    private static string? Location(JsonException e) => e.InnerException is JsonException inner ? inner.Path : e.Path;
}
