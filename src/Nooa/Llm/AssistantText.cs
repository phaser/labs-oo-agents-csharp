// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

namespace Nooa.Llm;

/// <summary>One assistant message, with immutable adapter-only JSON extensions.</summary>
public sealed record AssistantText : AssistantPart
{
    /// <summary>The message text.</summary>
    public required string Text { get; init; }
}
