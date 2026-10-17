// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Snapshot of the static system-role content for an LLM call. The runtime emits it right after it builds the
/// message list. It carries the rendered system prompt (strategy prompt, agent doc and static context blocks).
/// The trailing dynamic context is captured on <see cref="Nooa.Llm.LlmResponse.DynamicContext"/>.
/// It has the runtime-event role, so it stays out of the model context: it is pure observability data.
/// </summary>
public sealed record SystemPrompt() : EventBase(Role.RuntimeEvent)
{
    /// <summary>
    /// Rendered static system content for the LLM call about to happen. It is stable across the turns of one
    /// generation when all system-role blocks are static. A change indicates that a static block was mutated mid-run.
    /// The renderer must not truncate it.
    /// </summary>
    [MaxString]
    public string Content { get; init; } = "";

    /// <summary>
    /// Matches the generation identifier of the surrounding <see cref="BeforeTurn"/> and <see cref="AfterTurn"/>.
    /// Consumers use it to pair the snapshot with the right turn under concurrent generation.
    /// </summary>
    public string GenerationId { get; init; } = "";
}
