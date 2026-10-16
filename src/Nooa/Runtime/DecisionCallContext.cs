// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/middleware.py

namespace Nooa.Runtime;

/// <summary>
/// Context for middleware that wraps one decision-model call. Decisions are backlog item B03, so nothing runs this
/// chain yet. The request and the response are untyped until B03 adds the decision types.
/// </summary>
public sealed class DecisionCallContext
{
    /// <summary>The decision request. Middleware may replace it.</summary>
    public required object Request { get; set; }

    /// <summary>The agent that owns the runtime.</summary>
    public required Agent Agent { get; init; }

    /// <summary>The runtime that makes the call.</summary>
    public required ActorRuntime Runtime { get; init; }

    /// <summary>The decision response. Null on the way in.</summary>
    public object? Response { get; set; }
}
