// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>Everything the emitter needs for one <c>[Agentic]</c> method.</summary>
/// <param name="Name">The method name.</param>
/// <param name="Modifiers">The declaration modifiers except <c>partial</c>, for example <c>public</c>.</param>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="IsAwaitableWithResult">True for <c>Task&lt;T&gt;</c> and <c>ValueTask&lt;T&gt;</c>.</param>
/// <param name="ResultType">The declared <c>T</c>, for the generic call. Null when there is no result.</param>
/// <param name="ResultTypeOf">The <c>T</c> for a <c>typeof</c> expression. Null when there is no result.</param>
/// <param name="Parameters">All parameters, for the declaration.</param>
/// <param name="Prompt">The method doc comment as a prompt, or null.</param>
/// <param name="CancellationToken">Name of the trailing <c>CancellationToken</c> parameter, or null.</param>
/// <param name="Signature">The signature text for <c>CurrentCall.Signature</c>.</param>
/// <param name="StrategyTypeOf">The <c>typeof</c> expression of the <c>[Strategy]</c> type, or null.</param>
/// <param name="Arguments">The model-visible parameters: all except the trailing token.</param>
internal sealed record MethodModel(
    string Name,
    string Modifiers,
    string ReturnType,
    bool IsAwaitableWithResult,
    string? ResultType,
    string? ResultTypeOf,
    EquatableArray<ParameterModel> Parameters,
    string? CancellationToken,
    PromptModel? Prompt,
    string Signature,
    string? StrategyTypeOf,
    EquatableArray<ParameterModel> Arguments);
