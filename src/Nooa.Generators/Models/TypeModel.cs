// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>Everything the emitter needs for one agent class.</summary>
/// <param name="Key">A unique display name, used to group the results of both pipelines.</param>
/// <param name="Name">The class name.</param>
/// <param name="Namespace">The namespace, or null for the global namespace.</param>
/// <param name="Containers">The containing types, outermost first.</param>
/// <param name="Keyword"><c>class</c> or <c>record</c>.</param>
/// <param name="Llm">The <c>[AgentOptions]</c> model alias, or null.</param>
/// <param name="SystemPrompt">The class doc comment as a prompt, or null.</param>
/// <param name="TypeDoc">The class doc text for the doc provider, or null.</param>
/// <param name="Members">The members that have doc comments, in source order.</param>
/// <param name="Diagnostics">Diagnostics about the type itself. A type with diagnostics is not emitted.</param>
internal sealed record TypeModel(
    string Key,
    string? Namespace,
    EquatableArray<ContainerModel> Containers,
    string Keyword,
    string Name,
    string? Llm,
    PromptModel? SystemPrompt,
    string? TypeDoc,
    EquatableArray<MemberDocModel> Members,
    EquatableArray<DiagnosticInfo> Diagnostics);
