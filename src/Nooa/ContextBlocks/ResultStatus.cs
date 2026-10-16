// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

namespace Nooa.ContextBlocks;

/// <summary>Status of a tool result. Persisted as <c>running</c>, <c>complete</c>, <c>error</c> or <c>cancelled</c>.</summary>
public enum ResultStatus
{
    /// <summary>The call is still running.</summary>
    Running,

    /// <summary>The call finished normally.</summary>
    Complete,

    /// <summary>The call failed.</summary>
    Error,

    /// <summary>The call was interrupted by cancellation, for example a user cancel.</summary>
    Cancelled,
}
