// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Overrides the generation strategy of one agent method. The generator records the type in
/// <c>CurrentCall.StrategyType</c>. The runtime resolves it.
/// </summary>
/// <param name="strategyType">The strategy type.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StrategyAttribute(Type strategyType) : Attribute
{
    /// <summary>The strategy type.</summary>
    public Type StrategyType { get; } = strategyType;
}
