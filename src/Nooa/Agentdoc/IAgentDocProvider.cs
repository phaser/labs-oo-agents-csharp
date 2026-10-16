// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>
/// Supplies the doc comment text that reflection cannot read. The source generator (T11) emits an implementation
/// from the XML doc comments. Every method returns null when it has no text.
/// </summary>
public interface IAgentDocProvider
{
    /// <summary>
    /// Returns the summary text of a type, field, property or method. Multi-line text is allowed.
    /// </summary>
    string? GetSummary(MemberInfo member);

    /// <summary>
    /// Returns the description of a parameter, or of the return value when <paramref name="parameter"/> is the
    /// return parameter of a method.
    /// </summary>
    string? GetParameterSummary(ParameterInfo parameter);
}
