// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

namespace Nooa.Llm;

/// <summary>
/// Ends the stable prefix in a message list. Renderers and middleware pass it through unchanged.
/// The client consumes it after it projects the assistant turns. It is never sent to a model.
/// </summary>
public sealed record CacheBoundary : LlmMessage;
