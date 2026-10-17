// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/base.py

using Nooa.Config;
using Nooa.Llm;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// What a strategy can use from the runtime. A strategy gets this interface and not the whole runtime.
/// </summary>
/// <remarks>
/// Nested agentic calls need no call here: generated code calls <c>await this.Other()</c> and the runtime runs
/// the call inline inside the generation session, so there is no deadlock. Code execution (T17), variable
/// expansion and decisions (B03) are added with their strategies.
/// </remarks>
public interface IRuntimeServices
{
    /// <summary>The agent that runs the call.</summary>
    Agent Agent { get; }

    /// <summary>The event manager of the agent.</summary>
    EventManager EventManager { get; }

    /// <summary>The truncation config of the current call.</summary>
    TruncationConfig TruncationConfig { get; }

    /// <summary>The call that the strategy generates, or null outside a generation session.</summary>
    CurrentCall? CurrentCall { get; }

    /// <summary>The id of the innermost generation session, or null outside a session.</summary>
    string? GenerationId { get; }

    /// <summary>The id of the parent generation session, or null at the root.</summary>
    string? ParentGenerationId { get; }

    /// <summary>
    /// Sends one request to the effective LLM of the call and records the response as an event.
    /// Middleware for <see cref="LlmCallContext"/> wraps the round-trip.
    /// </summary>
    /// <param name="messages">The messages of the request. The strategy builds them (see the context renderer, T13).</param>
    /// <param name="tools">The tools that the model may call, or null.</param>
    /// <param name="outputType">The structured output type, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response and the tag of its event.</returns>
    Task<GenerateResult> GenerateAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs another strategy inside the current generation session, for a strategy that wraps others.
    /// It shares the lock and the events of the session.
    /// </summary>
    Task<object?> ExecuteNestedAsync(GenerationStrategy strategy, CurrentCall call, CancellationToken cancellationToken = default);
}
