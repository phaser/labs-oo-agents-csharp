// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Task prompt event, added at the start of generation. Persisted as <c>Task</c>.
/// </summary>
/// <remarks>
/// The type is named <c>TaskEvent</c> because <c>Task</c> would clash with <see cref="System.Threading.Tasks.Task"/>.
/// The renderer must show the name <c>Task</c> to the model (see T13).
/// </remarks>
public sealed record TaskEvent() : EventBase(Role.User)
{
    /// <summary>Task prompt that describes what to do. The renderer must not truncate it.</summary>
    [MaxString]
    public required string Prompt { get; init; }

    /// <summary>Multimodal content blocks (images, audio, files) attached to this task. Not shown to the model.</summary>
    [Hidden]
    public IReadOnlyList<JsonNode> Images { get; init; } = [];
}
