// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/snapshot.py

using System.Text.Json.Nodes;

namespace Nooa.Storage;

/// <summary>A static context block of a snapshot, with a JSON-like value.</summary>
/// <param name="Key">The block key.</param>
/// <param name="Value">The block value.</param>
/// <param name="IsStatic">True when the block is in the static (cacheable) partition.</param>
public sealed record SnapshotContextBlock(string Key, JsonNode? Value, bool IsStatic = false);
