// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/middleware.py

using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// Context for middleware that wraps an agentic method call: all turns and the final return.
/// Only calls that go through <see cref="IAgenticTarget"/> are covered. A plain C# method of the agent is not.
/// </summary>
public sealed class AgentCallContext
{
    /// <summary>The agent that runs the call.</summary>
    public required Agent Agent { get; init; }

    /// <summary>The name of the method.</summary>
    public required string MethodName { get; init; }

    /// <summary>The call arguments. Middleware may replace the list.</summary>
    public IReadOnlyList<CallArgument> Arguments { get; set; } = [];

    /// <summary>True after the innermost handler or a short-circuiting middleware set <see cref="Result"/>.</summary>
    public bool HasResult { get; private set; }

    /// <summary>The method result. Null on the way in. The innermost handler sets it on the way out.</summary>
    public object? Result
    {
        get;
        set
        {
            field = value;
            HasResult = true;
        }
    }
}
