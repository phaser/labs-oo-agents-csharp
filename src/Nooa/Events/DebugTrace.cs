// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Debug information stored in traces but never shown to the model.
/// Use it for raw LLM responses, internal state or diagnostics that must not influence generation.
/// </summary>
public sealed record DebugTrace() : EventBase(Role.Metadata)
{
    /// <summary>Debug information for trace inspection.</summary>
    public required string Content { get; init; }
}
