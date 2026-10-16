// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

namespace Nooa.ContextBlocks;

/// <summary>Status of an event in the event manager. Persisted as <c>active</c> or <c>archived</c>.</summary>
public enum EventStatus
{
    /// <summary>The event is part of the live context.</summary>
    Active,

    /// <summary>The event is collapsed or archived.</summary>
    Archived,
}
