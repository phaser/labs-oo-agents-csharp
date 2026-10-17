// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using Nooa.ContextBlocks;

namespace Nooa.Events;

/// <summary>
/// A range of events that was archived, with optional summary text.
/// With <see cref="SummaryText"/> set, the text is the LLM summary of the collapsed events.
/// With <see cref="SummaryText"/> null, the events were collapsed without a summary (truncation).
/// </summary>
/// <remarks>
/// Tags are strings: single events use <c>1</c>, <c>2</c>, ...; summary events use a range such as <c>2..40</c>.
/// </remarks>
public sealed record Summary() : EventBase(Role.Assistant)
{
    /// <summary>String tag for this summary, for example <c>2..40</c>.</summary>
    public required string SummaryTag { get; init; }

    /// <summary>The original (start, end) sequence range, for view ordering.</summary>
    public required SeqRange ReplacedRange { get; init; }

    /// <summary>Tags of the directly replaced events. Not shown to the model.</summary>
    [Hidden]
    public IReadOnlyList<string> ChildrenTags { get; init; } = [];

    /// <summary>LLM-generated summary, or null for truncation. The renderer must not truncate it.</summary>
    [MaxString]
    public string? SummaryText { get; init; }

    /// <summary>Usage hint for accessing the collapsed events.</summary>
    public string Doc { get; init; } = "";
}
