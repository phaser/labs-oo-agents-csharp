// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/models.py

namespace Nooa.ContextBlocks;

/// <summary>Typed metadata of a resolved block. Formatters read it to render the block.</summary>
/// <param name="Tag">The position tag of an event block, for example <c>5</c> or <c>2..40</c>.</param>
/// <param name="Truncated">True when the content was replaced because the block was over budget.</param>
/// <param name="UserBlock">True for a block that the user set in the context. Eviction drops user blocks first.</param>
/// <param name="Static">The author declares that the content is stable across turns. The cached layout puts it in the prefix.</param>
public sealed record BlockMetadata(string? Tag = null, bool Truncated = false, bool UserBlock = false, bool Static = false);
