// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (_handle_return_result, _maybe_parse_json_string, _try_validate_return_value)

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.Errors;
using Nooa.Json;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// Checks the value of <c>return_result</c> (the tool call or <c>ReturnResult</c> in a cell) against the return type of the
/// method and runs the postconditions. The model often passes the wrong shape, so the check is forgiving where it is safe:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Keyword-style arguments (no <c>result</c> key) become the result object.</item>
/// <item>A string that holds a variable name gives the value of that variable (pass-by-reference of a cell variable).</item>
/// <item>A string that holds a JSON object or array is parsed.</item>
/// <item>A value of another type is converted through JSON, as the serializer reads it (an <c>int</c> becomes a <c>double</c>, a dictionary becomes a record).</item>
/// </list>
/// <para>
/// Upstream also evaluated a constructor-call string such as <c>"Answer(answer=1)"</c> with a small AST evaluator. That path is not
/// ported: a string is never evaluated as code. A JSON object string covers the same case.
/// </para>
/// </remarks>
internal sealed class ReturnResultValidator(
    Agent agent, CurrentCall call, IExecutionEngine engine, CodeActConfig config, FormatConfig format)
{
    private readonly Type? _returnType = call.ReturnType;

    /// <summary>Checks the arguments of a <c>return_result</c> tool call.</summary>
    public ReturnCheck CheckToolArguments(JsonObject args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (_returnType is null)
        {
            return args["result"] is null ? Postconditions(null) : NothingExpected(args["result"]);
        }

        // The model may pass the fields of the result as arguments: wrap them.
        var node = args.ContainsKey("result") ? args["result"] : args.Count > 0 ? args : null;
        if (node is null && !args.ContainsKey("result"))
        {
            return ReturnCheck.Fail("return_result() failed: the required parameter 'result' is missing.");
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return Check(UnwrapDoubleQuotes(text));
        }

        return FromJson(node);
    }

    /// <summary>Checks a value that a cell passed to <c>ReturnResult</c> or returned.</summary>
    public ReturnCheck Check(object? value)
    {
        if (_returnType is null)
        {
            return value is null ? Postconditions(null) : NothingExpected(value);
        }

        if (value is string text && _returnType != typeof(string))
        {
            value = ResolveText(text);
        }

        return value switch
        {
            JsonNode node => FromJson(node),
            JsonElement element => FromJson(JsonNode.Parse(element.GetRawText())),
            _ => Typed(value),
        };
    }

    /// <summary>The arguments of the tool call as they should have been: <c>{"result": value}</c>. Falls back to the original.</summary>
    public static JsonObject CorrectedArguments(object? validated, JsonObject original)
    {
        if (validated is null)
        {
            return original;
        }

        try
        {
            return new JsonObject { ["result"] = JsonSerializer.SerializeToNode(validated, validated.GetType(), NooaJson.Options) };
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return original;
        }
    }

    private static string UnwrapDoubleQuotes(string text)
    {
        // A model sometimes wraps a string in extra quotes with escaped newlines. Unwrap it.
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"' && text.Contains("\\n", StringComparison.Ordinal))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(text) ?? text;
            }
            catch (JsonException)
            {
                return text;
            }
        }

        return text;
    }

    private object? ResolveText(string text)
    {
        // A name of a cell variable is a reference: return_result("results") means the variable results.
        if (SyntaxFacts.IsValidIdentifier(text) && engine.TryGetVariable(text, out var variable))
        {
            return variable;
        }

        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                return JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                // Not JSON: the string stays a string and fails the type check with a clear message.
            }
        }

        return text;
    }

    private ReturnCheck FromJson(JsonNode? node)
    {
        var target = Nullable.GetUnderlyingType(_returnType!) ?? _returnType!;
        try
        {
            var value = node.Deserialize(target, NooaJson.Options);
            return value is null && !AcceptsNull() ? ReturnCheck.Fail(CodeActErrors.WrongType(_returnType!, null, format)) : Typed(value);
        }
        catch (JsonException e)
        {
            return ReturnCheck.Fail(CodeActErrors.JsonMismatch(_returnType!, e.Path, e.Message, node, format));
        }
        catch (NotSupportedException)
        {
            return ReturnCheck.Fail(CodeActErrors.NotJson(_returnType!));
        }
    }

    private ReturnCheck Typed(object? value)
    {
        var type = _returnType!;
        if (value is null)
        {
            return AcceptsNull() ? Postconditions(null) : ReturnCheck.Fail(CodeActErrors.WrongType(type, null, format));
        }

        if (!type.IsInstanceOfType(value))
        {
            try
            {
                var node = JsonSerializer.SerializeToNode(value, value.GetType(), NooaJson.Options);
                value = node.Deserialize(Nullable.GetUnderlyingType(type) ?? type, NooaJson.Options);
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
            {
                return ReturnCheck.Fail(CodeActErrors.WrongType(type, value, format));
            }

            if (value is null || !type.IsInstanceOfType(value))
            {
                return ReturnCheck.Fail(CodeActErrors.WrongType(type, value, format));
            }
        }

        return Postconditions(value);
    }

    private bool AcceptsNull() => !_returnType!.IsValueType || Nullable.GetUnderlyingType(_returnType) is not null;

    private ReturnCheck NothingExpected(object? value) =>
        ReturnCheck.Fail(
            $"Method returns nothing but got result={PFormat.Format(value, format)}. Call return_result() with no arguments.");

    private ReturnCheck Postconditions(object? value)
    {
        try
        {
            StrategyConditions.RunPostconditions(agent, value, call, config.Postconditions);
            return ReturnCheck.Ok(value);
        }
        catch (InvariantException e)
        {
            return ReturnCheck.Fail(CodeActErrors.InvariantFailed(e.Message));
        }
    }
}
