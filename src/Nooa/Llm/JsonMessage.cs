// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.Json.Nodes;

namespace Nooa.Llm;

/// <summary>An OpenAI-style chat message, such as <c>{"role": "user", "content": "..."}</c>.</summary>
/// <param name="Value">The message object. The client does not change it.</param>
public sealed record JsonMessage(JsonObject Value) : LlmMessage;
