// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Excludes a field, property or method from agent documentation (<c>AgentDocs.Doc</c>) and from object rendering.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class HiddenAttribute : Attribute
{
}
