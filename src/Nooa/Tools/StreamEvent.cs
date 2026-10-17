// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/_results.py

namespace Nooa.Tools;

/// <summary>A chunk of command output.</summary>
/// <param name="Stream">The stream that produced the chunk.</param>
/// <param name="Text">The chunk text.</param>
public sealed record StreamEvent(StreamName Stream, string Text) : ShellStreamEvent;
