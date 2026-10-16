// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

namespace Nooa.ContextBlocks;

/// <summary>Assistant response event.</summary>
public sealed record AssistantEvent() : EventBase(Role.Assistant)
{
    /// <summary>Response content from the assistant.</summary>
    public required string Content { get; init; }
}
