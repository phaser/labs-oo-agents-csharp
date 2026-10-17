// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/models.py

namespace Nooa.ContextBlocks;

/// <summary>
/// A block with all content evaluated, ready to render. The renderer formats it and evaluates nothing.
/// An event block carries the original event, so the formatter can read structured data such as tool calls.
/// </summary>
/// <param name="Key">Unique identifier of the block.</param>
/// <param name="Content">The resolved text. It is empty for an event block until the renderer fills it.</param>
/// <param name="Role"><see cref="ContextBlocks.Role.System"/> for the system prompt, otherwise the message role.</param>
/// <param name="Metadata">Metadata for rendering.</param>
/// <param name="Event">The original event, when the block represents one.</param>
public sealed record ResolvedBlock(
    string Key,
    string Content,
    Role Role = Role.System,
    BlockMetadata? Metadata = null,
    EventBase? Event = null)
{
    /// <summary>The metadata of the block. A block built without metadata has the default values.</summary>
    public BlockMetadata Metadata { get; init; } = Metadata ?? new BlockMetadata();
}
