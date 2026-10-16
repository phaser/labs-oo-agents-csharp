// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/serialization.py

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Nooa.Storage;

/// <summary>
/// The type policy of snapshots (decision D11). A snapshot restores a value only into a declared member whose type
/// is eligible here. The snapshot file never names a type to create. Ordinary classes are not eligible unless they
/// carry <see cref="SnapshotableAttribute"/>, so a file cannot reach a type such as a stream or a process.
/// </summary>
internal static class SnapshotTypes
{
    private const BindingFlags _members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly HashSet<Type> _simple =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(string),
        typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
        typeof(JsonNode), typeof(JsonObject), typeof(JsonArray), typeof(JsonValue),
    ];

    private static readonly HashSet<Type> _listLike =
    [
        typeof(List<>), typeof(IList<>), typeof(IReadOnlyList<>), typeof(ICollection<>), typeof(IReadOnlyCollection<>),
        typeof(IEnumerable<>), typeof(HashSet<>), typeof(ISet<>),
    ];

    private static readonly HashSet<Type> _dictionaryLike =
    [
        typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
    ];

    /// <summary>One agent member that a snapshot saves and restores.</summary>
    public sealed record SnapshotMember(string Name, FieldInfo Field)
    {
        public Type Type => Field.FieldType;
    }

    /// <summary>The name that goes into the allowlist and the file for a declared type.</summary>
    public static string NameOf(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    /// <summary>
    /// The instance fields that a snapshot covers: those declared by the agent classes below <see cref="Agent"/>.
    /// A field or property marked <see cref="NoSnapshotAttribute"/>, and a delegate, is left out. A backing field
    /// of an auto-property uses the property name. A nearer class wins a name clash.
    /// </summary>
    public static IReadOnlyDictionary<string, SnapshotMember> MembersOf(Type agentType)
    {
        var result = new Dictionary<string, SnapshotMember>(StringComparer.Ordinal);
        for (var type = agentType; type is not null && type != typeof(Agent); type = type.BaseType)
        {
            foreach (var field in type.GetFields(_members))
            {
                var name = field.Name;
                MemberInfo marker = field;
                if (name.StartsWith('<') && name.EndsWith(">k__BackingField", StringComparison.Ordinal))
                {
                    name = name[1..name.IndexOf('>')];
                    marker = type.GetProperty(name, _members) ?? marker;
                }
                else if (field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    continue;
                }

                if (Attribute.IsDefined(marker, typeof(NoSnapshotAttribute), true)
                    || Attribute.IsDefined(field, typeof(NoSnapshotAttribute), true)
                    || typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                result.TryAdd(name, new SnapshotMember(name, field));
            }
        }

        return result;
    }

    /// <summary>True when a value of this class opts out of snapshots (it carries <see cref="NoSnapshotAttribute"/>).</summary>
    public static bool IsNoSnapshotValue(object? value) =>
        value is not null && Attribute.IsDefined(value.GetType(), typeof(NoSnapshotAttribute), true);

    /// <summary>True when a member of this declared type can be saved and restored.</summary>
    public static bool IsEligible(Type type) => IsEligible(type, []);

    private static bool IsEligible(Type type, HashSet<Type> visiting)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (_simple.Contains(type) || type.IsEnum)
        {
            return true;
        }

        if (Attribute.IsDefined(type, typeof(NoSnapshotAttribute), true) || type.IsPointer || type.IsByRef || type.IsGenericTypeParameter)
        {
            return false;
        }

        if (type.IsArray)
        {
            return type.GetArrayRank() == 1 && IsEligible(type.GetElementType()!, visiting);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (_listLike.Contains(definition))
            {
                return IsEligible(args[0], visiting);
            }

            if (_dictionaryLike.Contains(definition))
            {
                return args[0] == typeof(string) && IsEligible(args[1], visiting);
            }
        }

        if (type.Namespace is { } ns && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)))
        {
            return false;
        }

        if (!visiting.Add(type))
        {
            return true;
        }

        if (type.IsAbstract || type.IsInterface)
        {
            var derived = type.GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToList();
            return derived.Count > 0 && derived.All(d => IsEligible(d, visiting));
        }

        var plain = type.IsValueType || IsRecord(type) || Attribute.IsDefined(type, typeof(SnapshotableAttribute), false);
        return plain
            && type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.GetIndexParameters().Length == 0
                && !p.IsDefined(typeof(JsonIgnoreAttribute), true)).All(p => IsEligible(p.PropertyType, visiting))
            && type.GetFields(BindingFlags.Instance | BindingFlags.Public).All(f => IsEligible(f.FieldType, visiting));
    }

    private static bool IsRecord(Type type) => type.IsClass && type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is not null;
}
