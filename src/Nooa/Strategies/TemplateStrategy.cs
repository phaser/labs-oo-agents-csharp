// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/template.py

using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// Renders the method prompt without a model call. It returns the text of the method doc comment.
/// </summary>
/// <remarks>
/// Upstream expands <c>{expression}</c> placeholders at run time through <c>runtime.expand_variables</c>, with the
/// method parameters, <c>self</c> and <c>call</c> in scope. Here the generated method body expands the placeholders
/// when it builds the <see cref="CurrentCall"/> (decision D5), so <see cref="CurrentCall.Docstring"/> is already the
/// final text and this strategy returns it. Use it for a method that only builds text.
/// </remarks>
public sealed class TemplateStrategy : GenerationStrategy
{
    /// <inheritdoc />
    public override string Name => "TEMPLATE";

    /// <summary>A template is not a model call, so it leaves no trace.</summary>
    public override bool Traceable => false;

    /// <summary>A template needs no model call, so it can run concurrently.</summary>
    public override bool RequiresLock => false;

    /// <inheritdoc />
    public override Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        return Task.FromResult<object?>(call.Docstring ?? "");
    }
}
