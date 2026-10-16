// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/middleware.py

namespace Nooa.Runtime;

/// <summary>
/// Context for middleware that wraps one code execution. The REPL (T17, T18) runs the chain.
/// The context is minimal until then.
/// </summary>
public sealed class ExecuteCodeContext
{
    /// <summary>The source that is about to run. Middleware may replace it.</summary>
    public required string Code { get; set; }

    /// <summary>The agent that owns the runtime.</summary>
    public required Agent Agent { get; init; }

    /// <summary>The runtime that runs the code.</summary>
    public required ActorRuntime Runtime { get; init; }

    /// <summary>The execution result. Null on the way in. The innermost handler sets it on the way out.</summary>
    public object? Result { get; set; }
}
