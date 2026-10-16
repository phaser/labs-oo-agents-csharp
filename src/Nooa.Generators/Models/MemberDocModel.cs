// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>The doc text of one member, with what the doc provider needs to find the member by reflection.</summary>
/// <param name="Kind">One of <c>Field</c>, <c>Property</c>, <c>Method</c>.</param>
/// <param name="Name">The member name.</param>
/// <param name="ParameterTypes">For a method, the <c>typeof</c> expression of each parameter.</param>
/// <param name="Text">The summary and remarks. Null when only parameter docs exist.</param>
/// <param name="Params">Parameter docs by position. Position -1 is the return value.</param>
internal sealed record MemberDocModel(
    string Kind,
    string Name,
    EquatableArray<string> ParameterTypes,
    string? Text,
    EquatableArray<ParamDocModel> Params);
