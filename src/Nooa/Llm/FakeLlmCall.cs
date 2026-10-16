// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/fake.py

namespace Nooa.Llm;

/// <summary>Read-only snapshot of one call made through <see cref="FakeLlmClient"/>.</summary>
/// <param name="Index">The 1-based position of the call.</param>
/// <param name="Messages">The messages as sent. JSON messages are copies.</param>
/// <param name="Tools">The tools as sent, or null.</param>
/// <param name="OutputType">The requested output type, or null.</param>
/// <param name="Response">A copy of the response that the call returned, or null when it failed.</param>
/// <param name="Error">The exception that the call raised, or null.</param>
public sealed record FakeLlmCall(
    int Index,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools,
    Type? OutputType,
    LlmResponse? Response,
    Exception? Error);
