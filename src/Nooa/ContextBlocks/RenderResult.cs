// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/renderer.py

using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>The result of <see cref="ContextRenderer.Render"/>: the request messages and the utilization stats.</summary>
/// <param name="Messages">The messages in request order.</param>
/// <param name="Stats">Structural stats. The runtime adds the provider token count after the call.</param>
public sealed record RenderResult(IReadOnlyList<LlmMessage> Messages, ContextWindowStats Stats);
