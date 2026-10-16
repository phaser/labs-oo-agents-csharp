// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

namespace Nooa.Llm;

/// <summary>
/// NOOA-normalized outcome of a response. Provider-specific finish reasons collapse into these four portable
/// values. Persisted as <c>stop</c>, <c>tool_calls</c>, <c>length</c> or <c>error</c>.
/// </summary>
public enum FinishReason
{
    /// <summary>The model stopped normally.</summary>
    Stop,

    /// <summary>The model requested tool calls.</summary>
    ToolCalls,

    /// <summary>The response hit a length limit.</summary>
    Length,

    /// <summary>The call failed.</summary>
    Error,
}
