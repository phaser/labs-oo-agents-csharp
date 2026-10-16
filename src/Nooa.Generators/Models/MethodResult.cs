// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>The result of the method pipeline: the containing type, and either a model or diagnostics.</summary>
internal sealed record MethodResult(TypeModel Type, MethodModel? Method, EquatableArray<DiagnosticInfo> Diagnostics);
