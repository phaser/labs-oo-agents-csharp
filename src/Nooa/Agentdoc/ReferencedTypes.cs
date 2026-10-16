// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/core.py, _discover.py

using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>Finds the custom types that the API of a type or method refers to.</summary>
internal static class ReferencedTypes
{
    /// <summary>
    /// True for user-defined types. Types in the <c>System</c> and <c>Microsoft</c> namespaces, generic
    /// parameters and compiler-generated types are not custom.
    /// </summary>
    public static bool IsCustom(Type type)
    {
        if (type.IsGenericParameter || type.HasElementType || Nullable.GetUnderlyingType(type) is not null)
        {
            return false;
        }

        var ns = type.Namespace ?? string.Empty;
        if (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)
            || ns == "Microsoft" || ns.StartsWith("Microsoft.", StringComparison.Ordinal))
        {
            return false;
        }

        return !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);
    }

    /// <summary>Returns the generic definition of a constructed generic type, or the type itself.</summary>
    public static Type Normalize(Type type) => type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;

    /// <summary>Custom types in the visible fields, properties and methods of <paramref name="type"/>.</summary>
    public static IReadOnlySet<Type> Discover(Type type)
    {
        var found = new HashSet<Type>();
        foreach (var member in MemberReader.GetFields(type))
        {
            Collect(member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType, found);
        }

        foreach (var method in MemberReader.GetMethods(type))
        {
            CollectMethod(method, found);
        }

        return found;
    }

    /// <summary>Custom types in the parameters and the return type of <paramref name="method"/>.</summary>
    public static IReadOnlySet<Type> Discover(MethodInfo method)
    {
        var found = new HashSet<Type>();
        CollectMethod(method, found);
        return found;
    }

    /// <summary>
    /// Collects referenced types breadth-first to <paramref name="depth"/> levels, skipping <paramref name="seen"/>.
    /// The result is sorted by name.
    /// </summary>
    public static IReadOnlyList<Type> Collect(IEnumerable<IReadOnlySet<Type>> seeds, ISet<Type> seen, int depth)
    {
        var all = new List<Type>();
        var frontier = new List<Type>();
        foreach (var seed in seeds)
        {
            foreach (var t in seed)
            {
                if (seen.Add(t))
                {
                    frontier.Add(t);
                }
            }
        }

        for (var level = 0; level < depth && frontier.Count > 0; level++)
        {
            all.AddRange(frontier);
            var next = new List<Type>();
            foreach (var t in frontier)
            {
                foreach (var nested in Discover(t))
                {
                    if (seen.Add(nested))
                    {
                        next.Add(nested);
                    }
                }
            }

            frontier = next;
        }

        return [.. all.OrderBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.FullName, StringComparer.Ordinal)];
    }

    private static void CollectMethod(MethodInfo method, HashSet<Type> found)
    {
        Collect(method.ReturnType, found);
        foreach (var parameter in method.GetParameters())
        {
            Collect(parameter.ParameterType, found);
        }
    }

    private static void Collect(Type type, HashSet<Type> found)
    {
        if (type.HasElementType)
        {
            Collect(type.GetElementType()!, found);
            return;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            Collect(underlying, found);
            return;
        }

        if (type.IsConstructedGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                Collect(arg, found);
            }
        }

        var normalized = Normalize(type);
        if (IsCustom(normalized))
        {
            found.Add(normalized);
        }
    }
}
