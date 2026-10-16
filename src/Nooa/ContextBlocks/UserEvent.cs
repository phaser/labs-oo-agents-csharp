// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

namespace Nooa.ContextBlocks;

/// <summary>User message event.</summary>
public sealed record UserEvent() : EventBase(Role.User)
{
    /// <summary>Message content.</summary>
    public required string Content { get; init; }
}
