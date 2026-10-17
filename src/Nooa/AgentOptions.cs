// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agent.py

using Microsoft.Extensions.Logging;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Storage;
using Nooa.Strategies;

namespace Nooa;

/// <summary>
/// Runtime settings that an agent takes in its constructor (decision D6). Static settings go in
/// <see cref="AgentOptionsAttribute"/>.
/// </summary>
/// <remarks>
/// The LLM comes from <see cref="Llm"/> or from <see cref="LlmAlias"/>, not both. With neither, the agent uses the
/// <c>[AgentOptions(Llm = ...)]</c> alias of its class (or a base class), then the LLM of the parent agent.
/// Upstream <c>decision_model</c>, <c>render_config</c>, <c>context</c> and <c>event_query</c> are not here yet.
/// </remarks>
public sealed record AgentOptions
{
    /// <summary>The LLM client of this agent instance.</summary>
    public ILlmClient? Llm { get; init; }

    /// <summary>A model alias for this agent instance. The agent resolves it through <see cref="LlmResolver"/>.</summary>
    public string? LlmAlias { get; init; }

    /// <summary>
    /// Turns model aliases into clients for this agent. Null uses <see cref="Agent.DefaultLlmResolver"/>.
    /// An alias with no resolver is an error.
    /// </summary>
    public ILlmClientResolver? LlmResolver { get; init; }

    /// <summary>The storage. Null uses <see cref="InMemoryStorageManager"/>, which cannot save snapshots.</summary>
    public IStorageManager? Storage { get; init; }

    /// <summary>Truncation settings. They merge over <see cref="TruncationConfig.Default"/>.</summary>
    public TruncationConfig? Truncation { get; init; }

    /// <summary>Execution guards, such as the nesting depth. Null uses the defaults.</summary>
    public ExecutionConfig? Execution { get; init; }

    /// <summary>The strategy for a method that has no <see cref="StrategyAttribute"/> and no call-level override.</summary>
    public GenerationStrategy? DefaultStrategy { get; init; }

    /// <summary>
    /// Configured strategy instances, matched by exact type. A <see cref="StrategyAttribute"/> type that has an instance
    /// here uses it. Other strategy types get a new instance from their parameterless constructor.
    /// </summary>
    public IReadOnlyList<GenerationStrategy>? Strategies { get; init; }

    /// <summary>
    /// Formats the context blocks into messages. Null uses <see cref="CachedBlockFormatter"/>, which keeps a stable
    /// prefix for provider caching.
    /// </summary>
    public BlockFormatter? BlockFormatter { get; init; }

    /// <summary>The default filter for the events that appear in the context. Null shows all active events.</summary>
    public EventQuery? EventQuery { get; init; }

    /// <summary>
    /// Overrides for context blocks, applied at construction, also for the protected blocks. A value is a plain value,
    /// a <see cref="Context"/>, a <see cref="DynamicContext"/>, or null to remove the block.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Context { get; init; }

    /// <summary>Receives warnings from handlers, hooks and cleanup. Null discards them.</summary>
    public ILogger? Logger { get; init; }
}
