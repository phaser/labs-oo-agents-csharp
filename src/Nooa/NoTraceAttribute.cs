// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Excludes a class or method from call tracing events. No behaviour yet.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class NoTraceAttribute : Attribute
{
}
