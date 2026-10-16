// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Agentdoc;

/// <summary>The items of a collection, copied out so the printer can slice a head and a tail.</summary>
/// <param name="Kind">The shape of the collection.</param>
/// <param name="TypeName">The C# name of the collection type, used in truncation markers.</param>
/// <param name="Items">The items of a list, tuple or set. Empty for a dictionary.</param>
/// <param name="Entries">The pairs of a dictionary. Empty for the other kinds.</param>
internal sealed record CollectionInfo(
    CollectionKind Kind,
    string TypeName,
    IReadOnlyList<object?> Items,
    IReadOnlyList<KeyValuePair<object?, object?>> Entries)
{
    /// <summary>The number of items or pairs.</summary>
    public int Count => Kind == CollectionKind.Dictionary ? Entries.Count : Items.Count;
}
