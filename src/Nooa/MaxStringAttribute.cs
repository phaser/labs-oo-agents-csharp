// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Overrides the string length limit of object rendering (<c>PFormat</c>) for one field or property.
/// Without an argument the limit is removed. This replaces the upstream <c>spec(max_string=...)</c> annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
public sealed class MaxStringAttribute : Attribute
{
    /// <summary>Removes the string length limit of the member.</summary>
    public MaxStringAttribute()
    {
    }

    /// <summary>Sets the string length limit of the member.</summary>
    /// <param name="limit">The maximum characters per string. It must be greater than 0.</param>
    public MaxStringAttribute(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        Limit = limit;
    }

    /// <summary>The limit, or null for no limit.</summary>
    public int? Limit { get; }
}
