// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/roles.py

namespace Nooa.ContextBlocks;

/// <summary>Provider role for message formatting. Maps to the OpenAI and Anthropic message roles.</summary>
public enum Role
{
    /// <summary>System prompt blocks.</summary>
    System,

    /// <summary>A user message.</summary>
    User,

    /// <summary>An assistant message.</summary>
    Assistant,

    /// <summary>A tool result.</summary>
    Tool,

    /// <summary>An internal runtime event that is never recorded.</summary>
    RuntimeEvent,

    /// <summary>A stored metadata event that is never shown to the model.</summary>
    Metadata,
}
