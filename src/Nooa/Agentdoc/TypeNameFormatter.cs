// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>Writes a <see cref="Type"/> as C# source text, for example <c>Task&lt;List&lt;string?&gt;&gt;</c>.</summary>
internal static class TypeNameFormatter
{
    private static readonly Dictionary<Type, string> _keywords = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(nint)] = "nint",
        [typeof(nuint)] = "nuint",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(void)] = "void",
    };

    /// <summary>Formats a type. <paramref name="nullability"/> adds the <c>?</c> of nullable reference types.</summary>
    public static string Format(Type type, NullabilityInfo? nullability = null)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return Format(underlying) + "?";
        }

        string name;
        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            name = Format(type.GetElementType()!, nullability?.ElementType) + "[" + new string(',', rank - 1) + "]";
        }
        else if (type.IsPointer)
        {
            name = Format(type.GetElementType()!) + "*";
        }
        else if (_keywords.TryGetValue(type, out var keyword))
        {
            name = keyword;
        }
        else if (type.IsGenericType)
        {
            name = FormatGeneric(type, nullability);
        }
        else
        {
            name = type.Name;
        }

        // Reflection reports unconstrained type parameters as nullable, so a generic parameter never gets '?'.
        if (!type.IsValueType && !type.IsGenericParameter && nullability?.ReadState == NullabilityState.Nullable)
        {
            name += "?";
        }

        return name;
    }

    private static string FormatGeneric(Type type, NullabilityInfo? nullability)
    {
        var all = type.GetGenericArguments();
        var tick = type.Name.IndexOf('`');
        var arity = tick < 0 ? all.Length : int.Parse(type.Name[(tick + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        var offset = all.Length - arity;
        var args = new List<string>(arity);
        for (var i = 0; i < arity; i++)
        {
            var argNullability = nullability is not null && nullability.GenericTypeArguments.Length == all.Length
                ? nullability.GenericTypeArguments[offset + i]
                : null;
            args.Add(Format(all[offset + i], argNullability));
        }

        if (type.IsConstructedGenericType && type.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true && arity > 1)
        {
            return "(" + string.Join(", ", args) + ")";
        }

        var baseName = tick < 0 ? type.Name : type.Name[..tick];
        return baseName + "<" + string.Join(", ", args) + ">";
    }
}
