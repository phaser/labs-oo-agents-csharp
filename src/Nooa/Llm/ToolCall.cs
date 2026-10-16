// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

namespace Nooa.Llm;

/// <summary>Provider-independent tool call, exactly as the model emitted it.</summary>
public sealed record ToolCall : AssistantPart
{
    /// <summary>Provider-assigned identifier that matches the tool result.</summary>
    public required string Id { get; init; }

    /// <summary>Name of the tool that the model requested.</summary>
    public required string Name { get; init; }

    /// <summary>Exact JSON argument string that the model emitted.</summary>
    public required string Arguments { get; init; }
}
