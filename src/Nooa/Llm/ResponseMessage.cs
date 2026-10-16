// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

namespace Nooa.Llm;

/// <summary>An earlier model response that the client replays into the request as an assistant turn.</summary>
/// <param name="Response">The response event.</param>
public sealed record ResponseMessage(LlmResponse Response) : LlmMessage;
