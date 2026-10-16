// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>Execution feedback when the target method is not yet defined.</summary>
public sealed record Feedback() : EventBase(Role.User)
{
    /// <summary>Execution feedback content.</summary>
    public required string Content { get; init; }
}
