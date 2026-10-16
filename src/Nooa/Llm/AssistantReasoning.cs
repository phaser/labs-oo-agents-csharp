// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

namespace Nooa.Llm;

/// <summary>Readable reasoning, with optional immutable adapter-only JSON extensions.</summary>
public sealed record AssistantReasoning : AssistantPart
{
    /// <summary>The readable reasoning text.</summary>
    public string Text { get; init; } = "";
}
