// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/events.py

using Nooa.ContextBlocks;

namespace Nooa.Runtime;

/// <summary>
/// Query past events by type, tag, text, or call ID. Compact context by collapsing events.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>self.Events</c> to search the conversation history: tool calls, outputs, tasks, errors.
/// All queries return events in chronological order. Tags are stable string labels ("1", "2", ...) assigned on insert.
/// Summaries use range tags ("1..22"). A query returns at most <see cref="MaxResults"/> events, the most recent ones.
/// </para>
/// <para>Query:</para>
/// <code>
/// self.Events.Query(limit: 50)                                          // recent 50
/// self.Events.Query(type: "Task")                                       // all task events
/// self.Events.Query(type: "CellOutput", executionStatus: "error", limit: 1)
/// self.Events.Query(callId: "abc123")                                   // events for one call
/// self.Events.Query(query: "error")                                     // text search
/// self.Events.Query(query: "error.*db", regex: true)                    // regex search
/// </code>
/// <para>Access by tag:</para>
/// <code>
/// self.Events.Get("5")                  // by tag, null if missing
/// self.Events["5"]                      // by tag, KeyNotFoundException if missing
/// self.Events[["2", "3", "4"]]          // several tags
/// self.Events.Contains("5")             // existence check
/// var summary = (Summary)self.Events["1..22"];
/// var children = self.Events.Get(summary.ChildrenTags);
/// </code>
/// <para>Compact history: <c>self.Events.Collapse("2", "40", "User discussed X")</c> archives the range and puts one summary in its place.
/// The originals stay readable by their tags. Without a text the range is only cut.</para>
/// <para>
/// A regular expression runs in linear time, so lookarounds and backreferences are not supported.
/// Load this library with <c>Doc(self.Events)</c>.
/// </para>
/// </remarks>
public sealed class EventsApi
{
    /// <summary>The most events that one query or one multi-tag read returns.</summary>
    public const int MaxResults = 200;

    /// <summary>The longest type, call id, status or tag in characters.</summary>
    public const int MaxKeyLength = 128;

    /// <summary>The longest search text in characters.</summary>
    public const int MaxQueryLength = 1000;

    /// <summary>The longest regular expression in characters.</summary>
    public const int MaxRegexLength = 256;

    /// <summary>The longest summary text in characters.</summary>
    public const int MaxSummaryLength = 20_000;

    private readonly EventManager _manager;

    /// <summary>Creates the API for an agent.</summary>
    public EventsApi(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _manager = agent.EventManager;
    }

    /// <summary>The event with this tag or id. Throws when there is none.</summary>
    /// <exception cref="KeyNotFoundException">No event has this tag or id.</exception>
    public EventBase this[string key] => Get(key) ?? throw new KeyNotFoundException($"No event with tag or id '{key}'");

    /// <summary>The events with these tags or ids, in the order given. Throws when one is missing.</summary>
    /// <exception cref="KeyNotFoundException">A key has no event.</exception>
    public IReadOnlyList<EventBase> this[IReadOnlyList<string> keys]
    {
        get
        {
            CheckKeys(keys);
            return [.. keys.Select(k => Get(k) ?? throw new KeyNotFoundException($"No event with tag or id '{k}'"))];
        }
    }

    /// <summary>
    /// Queries events with AND semantics. Results are in chronological order. When there are more than the limit, the most recent win.
    /// </summary>
    /// <param name="type">Event type, for example <c>Task</c>, <c>CellOutput</c> or <c>ToolCallEvent</c>.</param>
    /// <param name="callId">Call ID in the metadata of the event.</param>
    /// <param name="executionStatus">Status of a <c>CellOutput</c>, for example <c>error</c> or <c>complete</c>.</param>
    /// <param name="query">Text search: case-insensitive substring, or a regular expression when <paramref name="regex"/> is true.</param>
    /// <param name="regex">Read <paramref name="query"/> as a regular expression.</param>
    /// <param name="limit">The most results. The default and the maximum are <see cref="MaxResults"/>.</param>
    /// <exception cref="ArgumentException">An input is too long, the limit is below 1, or the regular expression is invalid or not supported.</exception>
    public IReadOnlyList<EventBase> Query(
        string? type = null,
        string? callId = null,
        string? executionStatus = null,
        string? query = null,
        bool regex = false,
        int? limit = null)
    {
        CheckLength(type, MaxKeyLength, nameof(type));
        CheckLength(callId, MaxKeyLength, nameof(callId));
        CheckLength(executionStatus, MaxKeyLength, nameof(executionStatus));
        CheckLength(query, regex ? MaxRegexLength : MaxQueryLength, nameof(query));
        if (limit is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit must be at least 1.");
        }

        try
        {
            return _manager.Filter(type, callId, executionStatus, query, regex, Math.Min(limit ?? MaxResults, MaxResults));
        }
        catch (NotSupportedException e)
        {
            throw new ArgumentException($"The regular expression uses an unsupported feature (lookaround or backreference): {e.Message}", nameof(query), e);
        }
    }

    /// <summary>The event with this tag or id, or null when there is none.</summary>
    /// <param name="key">A tag such as <c>5</c> or <c>1..22</c>, or an event id.</param>
    public EventBase? Get(string key)
    {
        CheckLength(key, MaxKeyLength, nameof(key));
        return _manager.Get(key);
    }

    /// <summary>The events with these tags or ids. A missing event is left out.</summary>
    public IReadOnlyList<EventBase> Get(IReadOnlyList<string> keys)
    {
        CheckKeys(keys);
        return [.. keys.Select(_manager.Get).OfType<EventBase>()];
    }

    /// <summary>True when an event has this tag or id.</summary>
    public bool Contains(string key) => Get(key) is not null;

    /// <summary>
    /// Archives a range of events into one summary marker. The originals stay readable by their tags.
    /// </summary>
    /// <param name="startTag">The first tag, inclusive, for example <c>2</c>. A summary tag such as <c>2..10</c> is allowed.</param>
    /// <param name="endTag">The last tag, inclusive, for example <c>40</c>.</param>
    /// <param name="summaryText">The summary, or null to cut the range with no text.</param>
    /// <returns>The tag of the summary, for example <c>2..40</c>.</returns>
    /// <exception cref="ArgumentException">A tag is not valid, no active event is in the range, or the text is too long.</exception>
    public string Collapse(string startTag, string endTag, string? summaryText = null)
    {
        CheckLength(startTag, MaxKeyLength, nameof(startTag));
        CheckLength(endTag, MaxKeyLength, nameof(endTag));
        CheckLength(summaryText, MaxSummaryLength, nameof(summaryText));
        return _manager.Collapse(startTag, endTag, summaryText);
    }

    /// <summary>Like <see cref="Collapse(string, string, string?)"/>, with numbers. Each number must identify an existing event.</summary>
    /// <exception cref="ArgumentException">A number does not identify an existing event.</exception>
    public string Collapse(int startTag, int endTag, string? summaryText = null)
    {
        foreach (var (name, tag) in new[] { (nameof(startTag), startTag), (nameof(endTag), endTag) })
        {
            if (_manager.Get(tag.ToString(System.Globalization.CultureInfo.InvariantCulture)) is null)
            {
                throw new ArgumentException($"{name} does not identify an existing event: {tag}", name);
            }
        }

        return Collapse(startTag.ToString(System.Globalization.CultureInfo.InvariantCulture), endTag.ToString(System.Globalization.CultureInfo.InvariantCulture), summaryText);
    }

    /// <summary>
    /// The active event tags in chronological order, for example <c>1</c>, <c>2..40</c>, <c>41</c>.
    /// After a collapse the range is one <c>start..end</c> tag.
    /// </summary>
    public IReadOnlyList<string> Keys() => _manager.Keys();

    /// <inheritdoc />
    public override string ToString() => $"<EventsApi({_manager.Keys().Count} active)>";

    private static void CheckKeys(IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count > MaxResults)
        {
            throw new ArgumentException($"At most {MaxResults} keys per call, got {keys.Count}.", nameof(keys));
        }

        foreach (var key in keys)
        {
            CheckLength(key, MaxKeyLength, nameof(keys));
        }
    }

    private static void CheckLength(string? text, int max, string name)
    {
        if (text is { } value && value.Length > max)
        {
            throw new ArgumentException($"{name} is longer than {max} characters.", name);
        }
    }
}
