// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// Generic "something happened" signal that is rendered into the model context.
/// </summary>
/// <remarks>
/// <para>
/// It is not tied to a producer. Add it yourself for job completions, timer ticks, webhook deliveries or any
/// other asynchronous signal that the agent must know about.
/// </para>
/// <para>
/// <see cref="Source"/> says who produced it, as a short namespaced string such as <c>queue:user_messages</c>,
/// or as a plain sentence when a session host appends a steering message. The model reads it.
/// </para>
/// </remarks>
public sealed record Notification() : EventBase(Role.User)
{
    /// <summary>Origin of the notification, for example <c>queue:user_messages</c>.</summary>
    public required string Source { get; init; }

    /// <summary>
    /// Human-readable description. The renderer may cut it at 20,000 characters (above the default event limit of 10,000).
    /// </summary>
    [MaxString(20_000)]
    public string Description { get; init; } = "";

    /// <summary>Optional data payload, as JSON. The caller must convert the value to JSON.</summary>
    public JsonNode? Value { get; init; }
}
