// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Excludes a field, a property or a class from state snapshots. A field or a property with this attribute is not
/// saved. A value whose class has this attribute is not saved either. <see cref="Agent"/> has it, so an agent
/// never nests inside another value.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = true)]
public sealed class NoSnapshotAttribute : Attribute
{
}
