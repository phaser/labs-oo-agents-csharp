// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_pformat.py

using System.Collections;
using System.Runtime.CompilerServices;

namespace Nooa.Agentdoc;

/// <summary>
/// Recognizes the collections that the value printer expands. It enumerates arrays, tuples, dictionaries,
/// sets and collections that report a count. It never enumerates a plain <see cref="IEnumerable"/>: a lazy
/// sequence can have side effects or never end.
/// </summary>
internal static class CollectionReader
{
    /// <summary>Copies the items of <paramref name="value"/>. Returns null when the value is not an expanded collection.</summary>
    public static CollectionInfo? TryRead(object value)
    {
        var type = value.GetType();
        if (value is ITuple tuple)
        {
            var items = new List<object?>(tuple.Length);
            for (var i = 0; i < tuple.Length; i++)
            {
                items.Add(tuple[i]);
            }

            return new CollectionInfo(CollectionKind.Tuple, "tuple", items, []);
        }

        if (value is IDictionary dictionary)
        {
            var entries = new List<KeyValuePair<object?, object?>>(dictionary.Count);
            foreach (DictionaryEntry entry in dictionary)
            {
                entries.Add(new(entry.Key, entry.Value));
            }

            return new CollectionInfo(CollectionKind.Dictionary, TypeNameFormatter.Format(type), [], entries);
        }

        var kind = Classify(value, type);
        if (kind is null)
        {
            return null;
        }

        var list = new List<object?>();
        foreach (var item in (IEnumerable)value)
        {
            list.Add(item);
        }

        return new CollectionInfo(kind.Value, TypeNameFormatter.Format(type), list, []);
    }

    private static CollectionKind? Classify(object value, Type type)
    {
        if (value is Array)
        {
            return CollectionKind.List;
        }

        var isCollection = value is ICollection;
        foreach (var contract in type.GetInterfaces())
        {
            if (!contract.IsConstructedGenericType)
            {
                continue;
            }

            var definition = contract.GetGenericTypeDefinition();
            if (definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>))
            {
                return CollectionKind.Set;
            }

            isCollection |= definition == typeof(ICollection<>) || definition == typeof(IReadOnlyCollection<>);
        }

        return isCollection ? CollectionKind.List : null;
    }
}
