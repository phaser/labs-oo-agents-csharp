// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_structured.py, _visibility.py

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nooa.Agentdoc;

/// <summary>
/// Lists the members that <c>Doc</c> shows: public instance fields and properties, and public methods.
/// Members named with a leading underscore, members marked <see cref="HiddenAttribute"/> and compiler-generated
/// members are left out. Members of <c>System</c> base types are left out too.
/// </summary>
/// <remarks>
/// Reflection has no source order. Members of one type sort by <see cref="MemberInfo.MetadataToken"/>, which follows
/// declaration order in a single file. Fields come before properties within one type.
/// </remarks>
internal static class MemberReader
{
    private const BindingFlags _instanceDeclared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>Fields and properties (<see cref="FieldInfo"/> or <see cref="PropertyInfo"/>). Base type members come first.</summary>
    public static IReadOnlyList<MemberInfo> GetFields(Type type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<List<MemberInfo>>();
        foreach (var t in Chain(type))
        {
            var group = new List<MemberInfo>();
            foreach (var field in t.GetFields(_instanceDeclared).OrderBy(f => f.MetadataToken))
            {
                if (!field.IsSpecialName && IsVisible(field) && seen.Add(field.Name))
                {
                    group.Add(field);
                }
            }

            foreach (var property in t.GetProperties(_instanceDeclared).OrderBy(p => p.MetadataToken))
            {
                if (property.GetIndexParameters().Length == 0 && IsVisible(property) && seen.Add(property.Name))
                {
                    group.Add(property);
                }
            }

            groups.Add(group);
        }

        groups.Reverse();
        return [.. groups.SelectMany(g => g)];
    }

    /// <summary>Public methods, derived type first. Overrides appear once.</summary>
    public static IReadOnlyList<MethodInfo> GetMethods(Type type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<MethodInfo>();
        foreach (var t in Chain(type))
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in t.GetMethods(flags).OrderBy(m => m.MetadataToken))
            {
                if (method.IsSpecialName || !IsVisible(method) || method.GetBaseDefinition().DeclaringType == typeof(object))
                {
                    continue;
                }

                var key = method.Name + "`" + method.GetGenericArguments().Length + "("
                    + string.Join(",", method.GetParameters().Select(p => p.ParameterType.ToString())) + ")";
                if (seen.Add(key))
                {
                    result.Add(method);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Reads a field, or the backing field of an auto-property. Returns false for a computed property: its getter
    /// can have side effects, and a virtual getter can hide a computed override. No user code runs.
    /// </summary>
    public static bool TryReadValue(MemberInfo member, object instance, out object? value)
    {
        value = null;
        if (member is FieldInfo field)
        {
            value = field.GetValue(instance);
            return true;
        }

        var property = (PropertyInfo)member;
        var backingField = property.GetMethod?.IsPublic == true
            ? property.DeclaringType!.GetField($"<{property.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            : null;
        if (backingField is null)
        {
            return false;
        }

        value = backingField.GetValue(instance);
        return true;
    }

    /// <summary>True when the member carries <see cref="HiddenAttribute"/>.</summary>
    public static bool IsHidden(MemberInfo member) => Attribute.IsDefined(member, typeof(HiddenAttribute), inherit: true);

    private static bool IsVisible(MemberInfo member)
    {
        return !member.Name.StartsWith('_') && !member.Name.StartsWith('<')
            && !member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && !IsHidden(member);
    }

    private static IEnumerable<Type> Chain(Type type)
    {
        for (Type? t = type; t is not null && ReferencedTypes.IsCustom(ReferencedTypes.Normalize(t)); t = t.BaseType)
        {
            yield return t;
        }
    }
}
