// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/prefill.py

using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// A prefill that prints the arguments of the call, each with <c>PPrint</c> and the limits of
/// <see cref="TruncationConfig.PrefillFormat"/>, so the model sees its inputs before it writes code. For a complex return
/// type it also prints the documentation of that type.
/// </summary>
/// <remarks>
/// The printed type of an argument is the declared type. Upstream printed the type of the value. The code avoids
/// <c>GetType()</c>, which the code validator refuses. Media arguments (<c>show()</c>) are not ported.
/// </remarks>
public sealed class InspectInputsPrefill : IPrefill
{
    /// <inheritdoc />
    public string? GetCode(CurrentCall call, TruncationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Arguments.Count == 0)
        {
            return null;
        }

        var format = (config ?? TruncationConfig.Default).PrefillFormat;
        var limits = $"maxLength: {Limit(format.MaxLength)}, maxString: {Limit(format.MaxString)}, maxDepth: {Limit(format.MaxDepth)}";
        var code = new StringBuilder();
        code.Append("// Inspecting inputs for ").Append(call.MethodName).Append("().\n");
        code.Append("Print(\"Task: ").Append(call.MethodName).Append("()\");\n");
        foreach (var argument in call.Arguments)
        {
            var type = TypeNameFormatter.Format(argument.Type);
            code.Append("Print(\"\\n").Append(argument.Name).Append(" (").Append(type).Append("):\");\n");
            code.Append("PPrint(").Append(IdentifierOf(argument.Name)).Append(", ").Append(limits).Append(");\n");
        }

        if (call.ReturnType is { } returnType && IsComplex(returnType))
        {
            code.Append("Print(\"\\nReturn type:\");\n");
            code.Append("Doc(typeof(").Append(CSharpTypeName.Of(returnType)).Append("), concise: true);\n");
        }

        return code.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// True when the type is worth a <c>Doc</c> call: not a scalar and not a collection.
    /// A nullable type counts as its underlying type. A collection counts as simple, whatever its element type.
    /// </summary>
    internal static bool IsComplex(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type.IsArray || type == typeof(string) || type == typeof(decimal) || type == typeof(object)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return false;
        }

        return !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    private static string Limit(int? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "null";

    private static string IdentifierOf(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;
}
