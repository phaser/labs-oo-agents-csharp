// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/sqlite.py

using System.Reflection;
using System.Text.Json.Serialization;
using Nooa.ContextBlocks;

namespace Nooa.Storage;

/// <summary>
/// The <c>event_type</c> names that storage may restore. It is the <see cref="JsonDerivedTypeAttribute"/> list of
/// <see cref="EventBase"/> (decision D11). There is no other registry.
/// </summary>
internal static class EventTypeAllowlist
{
    private static readonly HashSet<string> _names = typeof(EventBase)
        .GetCustomAttributes<JsonDerivedTypeAttribute>()
        .Select(a => (string)a.TypeDiscriminator!)
        .ToHashSet(StringComparer.Ordinal);

    public static bool Contains(string eventType) => _names.Contains(eventType);
}
