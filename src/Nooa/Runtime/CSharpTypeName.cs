// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace Nooa.Runtime;

/// <summary>Writes a type as a C# type name that a script can compile, with <c>global::</c> and without aliases.</summary>
internal static class CSharpTypeName
{
    public static string Of(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsPointer || type.IsByRef || type.IsGenericParameter || type.IsByRefLike)
        {
            throw new ArgumentException($"The type {type} cannot be written as a variable type.", nameof(type));
        }

        return Write(type);
    }

    private static string Write(Type type)
    {
        if (type.IsArray)
        {
            return Write(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Write(underlying) + "?";
        }

        return WriteNamed(type.IsGenericType ? type.GetGenericTypeDefinition() : type, type.GetGenericArguments());
    }

    // A nested type reports its declaring type as an open definition, so the arguments come from the constructed type.
    private static string WriteNamed(Type definition, Type[] arguments)
    {
        var sb = new StringBuilder();
        var outerCount = 0;
        if (definition.DeclaringType is { } declaring)
        {
            outerCount = declaring.GetGenericArguments().Length;
            sb.Append(WriteNamed(declaring, arguments[..outerCount])).Append('.');
        }
        else
        {
            sb.Append("global::");
            if (!string.IsNullOrEmpty(definition.Namespace))
            {
                sb.Append(definition.Namespace).Append('.');
            }
        }

        var name = definition.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        sb.Append(tick < 0 ? name : name[..tick]);
        var own = arguments[outerCount..];
        if (tick >= 0 && own.Length > 0)
        {
            sb.Append('<').Append(string.Join(", ", own.Select(Write))).Append('>');
        }

        return sb.ToString();
    }
}
