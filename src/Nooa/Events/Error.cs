// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>Error that is fed back to the model for a retry.</summary>
public sealed record Error() : EventBase(Role.User)
{
    /// <summary>Error message for the LLM retry.</summary>
    public required string Content { get; init; }
}
