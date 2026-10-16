// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>One parameter of an agentic method.</summary>
/// <param name="Name">The parameter name, escaped when it is a keyword.</param>
/// <param name="DeclaredType">The type with nullable annotations, for the method signature.</param>
/// <param name="TypeOf">The type for a <c>typeof</c> expression.</param>
/// <param name="Display">The <c>type name</c> text for <c>CurrentCall.Signature</c>.</param>
internal sealed record ParameterModel(string Name, string DeclaredType, string TypeOf, string Display);
