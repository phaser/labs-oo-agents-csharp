// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Agentdoc;

/// <summary>The shape of a collection that the value printer expands.</summary>
internal enum CollectionKind
{
    /// <summary>Ordered items: arrays, lists, queues. Printed as <c>[a, b]</c>.</summary>
    List,

    /// <summary>A tuple. Printed as <c>(a, b)</c>.</summary>
    Tuple,

    /// <summary>Unordered items. Printed as <c>{a, b}</c>.</summary>
    Set,

    /// <summary>Key and value pairs. Printed as <c>{"k": v}</c>.</summary>
    Dictionary,
}
