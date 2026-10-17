// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context_builder.py

using Nooa.ContextBlocks;

namespace Nooa.Runtime;

/// <summary>The result of <see cref="ContextBuilder.Build"/>: the blocks and the resolved values for the context manager cache.</summary>
/// <param name="Blocks">The system blocks, then one block for each event.</param>
/// <param name="ResolvedCache">The resolved text of each block with a provider. Pass it to <see cref="ContextManager.UpdateResolved"/>.</param>
internal sealed record BuildResult(IReadOnlyList<ResolvedBlock> Blocks, IReadOnlyDictionary<string, object?> ResolvedCache);
