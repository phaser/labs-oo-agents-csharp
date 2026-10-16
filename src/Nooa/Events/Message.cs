// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>User-facing message from generated code.</summary>
public sealed record Message() : EventBase(Role.Assistant)
{
    /// <summary>User-facing message content.</summary>
    public required string Content { get; init; }
}
