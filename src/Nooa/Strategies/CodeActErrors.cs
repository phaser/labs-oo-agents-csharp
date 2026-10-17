// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact_errors.py

using System.Reflection;
using Nooa.Agentdoc;
using Nooa.Config;

namespace Nooa.Strategies;

/// <summary>
/// Formats the failures of a returned value as feedback for the model. The texts name what is expected and what came back,
/// and show an example of the right call. Pydantic error codes have no equivalent: System.Text.Json reports the path of the
/// value that does not fit, and the formatter builds the rest from the expected type.
/// </summary>
internal static class CodeActErrors
{
    private const string _missingMarker = "missing required properties";

    /// <summary>The feedback for a postcondition that rejected the result.</summary>
    public static string InvariantFailed(string message) =>
        $"Method invariant failed: {message}\nRevise the answer so it satisfies this invariant, then call return_result() again.";

    /// <summary>The feedback for a value that has the wrong type.</summary>
    public static string WrongType(Type expected, object? actual, FormatConfig format)
    {
        var text = $"return_result() failed - wrong type.\nExpected: {ExpectedSchema(expected)}\n{GotLine(actual, format)}";
        return Example(expected) is { } example ? text + $"\nExample: ReturnResult({example})" : text;
    }

    /// <summary>The feedback for a JSON value that does not fit the type.</summary>
    /// <param name="expected">The expected type.</param>
    /// <param name="path">The JSON path of the failing value, or null.</param>
    /// <param name="serializerMessage">The message of the serializer. It is used only for a missing member.</param>
    /// <param name="actual">The value that came back.</param>
    /// <param name="format">The bounds for the shown value.</param>
    public static string JsonMismatch(Type expected, string? path, string? serializerMessage, object? actual, FormatConfig format)
    {
        var lines = new List<string>();
        if (serializerMessage?.Contains(_missingMarker, StringComparison.Ordinal) == true)
        {
            lines.Add("return_result() failed - missing required fields:");
            lines.Add($"Expected: {ExpectedSchema(expected)}");
            lines.Add(serializerMessage);
        }
        else
        {
            lines.Add($"return_result() failed - a value does not match the type at {path ?? "$"}.");
            lines.Add($"Expected: {ExpectedSchema(expected)}");
        }

        if (actual is not null)
        {
            lines.Add(GotLine(actual, format));
        }

        return string.Join('\n', lines);
    }

    /// <summary>The feedback for a type that cannot be passed through the tool, only built in code.</summary>
    public static string NotJson(Type expected) =>
        $"return_result() failed - {TypeNameFormatter.Format(expected)} cannot be passed as JSON.\n"
        + $"Hint: build the {TypeNameFormatter.Format(expected)} object in {CodeActPrompts.CodeToolName}, then call ReturnResult(variable) from within the code.";

    /// <summary>A compact description of a type: <c>Name { Field: type, ... }</c> for an object, else the type name.</summary>
    public static string ExpectedSchema(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        var name = TypeNameFormatter.Format(type);
        if (type.IsPrimitive || type.IsEnum || type.IsArray || type == typeof(string) || type == typeof(decimal) || type.IsGenericType
            || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return name;
        }

        var fields = MemberReader.GetFields(type)
            .Select(m => $"{m.Name}: {TypeNameFormatter.Format(m is PropertyInfo p ? p.PropertyType : ((FieldInfo)m).FieldType)}")
            .ToList();
        return fields.Count == 0 ? name : $"{name} {{ {string.Join(", ", fields)} }}";
    }

    /// <summary>An example argument of <c>ReturnResult</c> for a simple type, or null.</summary>
    public static string? Example(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            return "\"your answer\"";
        }

        if (type == typeof(int) || type == typeof(long))
        {
            return "42";
        }

        if (type == typeof(double))
        {
            return "3.14";
        }

        if (type == typeof(bool))
        {
            return "true";
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var item = type.GetGenericArguments()[0];
            return item == typeof(string) ? "new List<string> { \"item1\", \"item2\" }"
                : item == typeof(int) ? "new List<int> { 1, 2, 3 }"
                : $"new List<{TypeNameFormatter.Format(item)}> {{ ... }}";
        }

        return null;
    }

    private static string GotLine(object? actual, FormatConfig format) =>
        actual is null
            ? "Got: null"
            : $"Got: {TypeNameFormatter.Format(actual.GetType())} (value: {PFormat.Format(actual, format)})";
}
