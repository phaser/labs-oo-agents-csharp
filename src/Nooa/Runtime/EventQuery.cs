// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/event_query.py

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nooa.ContextBlocks;

namespace Nooa.Runtime;

/// <summary>
/// A declarative filter for the events that appear in the context. It mirrors the parameters of
/// <see cref="EventManager.Filter"/>. A query can be set for the agent, for a scope (<see cref="ScopedContext"/>) or
/// at run time (<see cref="EventManager.SetEventQuery"/>).
/// </summary>
/// <example>
/// <code>
/// EventQuery.CurrentCall()                              // events of the current call only
/// new EventQuery { Limit = 10 }                         // the last 10 events
/// EventQuery.ByType("Error")                            // error events only
/// new EventQuery { Type = "Task", CallId = "current", Limit = 5 }
/// </code>
/// </example>
public sealed record EventQuery
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The call id value that means the current call.</summary>
    public const string CurrentCallId = "current";

    /// <summary>Keeps only events of this type, for example <c>Task</c> or <c>Error</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Keeps only events of this call. <c>current</c> means the current call.</summary>
    public string? CallId { get; init; }

    /// <summary>Keeps only events whose public fields contain this text.</summary>
    public string? Query { get; init; }

    /// <summary>Reads <see cref="Query"/> as a regular expression. A match that takes over 1 second fails.</summary>
    public bool Regex { get; init; }

    /// <summary>Keeps only the last events, at most this many. It must be greater than 0.</summary>
    public int? Limit
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value ?? 1, 1);
            field = value;
        }
    }

    /// <summary>A query for the events of the current method call.</summary>
    public static EventQuery CurrentCall(int? limit = null) => new() { CallId = CurrentCallId, Limit = limit };

    /// <summary>A query for one event type.</summary>
    public static EventQuery ByType(string eventType, int? limit = null) => new() { Type = eventType, Limit = limit };

    /// <summary>A query for the last <paramref name="n"/> events.</summary>
    public static EventQuery LastN(int n) => new() { Limit = n };

    /// <summary>Applies the filters in order: call, type, text, limit.</summary>
    /// <param name="events">The events to filter.</param>
    /// <param name="currentCallId">The id that <c>current</c> stands for.</param>
    public IReadOnlyList<EventBase> Apply(IReadOnlyList<EventBase> events, string? currentCallId = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        IEnumerable<EventBase> filtered = events;
        if (CallId is not null)
        {
            var target = CallId == CurrentCallId ? currentCallId : CallId;
            filtered = filtered.Where(e => e.Metadata["call_id"] is JsonValue v && v.TryGetValue<string>(out var id) && id == target);
        }

        if (Type is not null)
        {
            filtered = filtered.Where(e => e.EventType == Type);
        }

        if (Query is not null)
        {
            if (Regex)
            {
                var pattern = new Regex(Query, RegexOptions.None, _regexTimeout);
                filtered = filtered.Where(e => pattern.IsMatch(EventRenderer.SearchableText(e)));
            }
            else
            {
                filtered = filtered.Where(e => EventRenderer.SearchableText(e).Contains(Query, StringComparison.OrdinalIgnoreCase));
            }
        }

        var result = filtered.ToList();
        return Limit is { } limit && result.Count > limit ? result[^limit..] : result;
    }
}
