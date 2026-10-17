// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/event_manager.py

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Json;

namespace Nooa.Runtime;

/// <summary>
/// The event pipeline of an agent: it notifies subscribers, records events with string tags and answers queries.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Add"/> notifies the subscribers, then records the event under a tag (<c>1</c>, <c>2</c>, or a range
/// such as <c>2..40</c> for a summary). The backend allocates the tag, so several managers on one backend never
/// give out the same tag. <see cref="Keys"/> returns only the active tags, the view that the model sees.
/// </para>
/// <para>
/// Middleware (<see cref="Intercept{TContext}"/>) wraps a live operation and can change or block it.
/// A handler (<see cref="On"/>) observes an event after the fact. The kinds of middleware are the context types
/// <see cref="AgentCallContext"/>, <see cref="LlmCallContext"/>, <see cref="DecisionCallContext"/> and
/// <see cref="ExecuteCodeContext"/>. The first registered middleware is the outermost.
/// </para>
/// <para>
/// Access to the backend is serialized with a lock, so parallel tasks of one agent can add events.
/// A handler runs outside the lock.
/// </para>
/// </remarks>
public sealed class EventManager
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly ILogger _logger;
    private readonly Dictionary<string, List<Action<EventBase>>> _handlers = [];
    private readonly Dictionary<Type, List<Delegate>> _middleware = [];
    private readonly List<Func<Task>> _closeCallbacks = [];
    private readonly AsyncLocal<bool> _inDrain = new();
    private IEventBackend _backend;
    private EventQuery? _eventQuery;
    private Task? _closeTask;

    /// <summary>Creates an event manager.</summary>
    /// <param name="backend">The storage backend. Null uses a new <see cref="InMemoryEventBackend"/>.</param>
    /// <param name="logger">Receives warnings for handler and cleanup failures. Null discards them.</param>
    public EventManager(IEventBackend? backend = null, ILogger? logger = null)
    {
        _backend = backend ?? new InMemoryEventBackend();
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The number of recorded events, active and archived.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _backend.Count;
            }
        }
    }

    /// <summary>Swaps the persistence backend. Handlers and middleware stay.</summary>
    public void SetBackend(IEventBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        lock (_gate)
        {
            _backend = backend;
        }
    }

    /// <summary>
    /// Adds an event: tags it with the current call id, notifies subscribers, and records it when
    /// <paramref name="record"/> is true. Runtime events are never recorded.
    /// </summary>
    /// <returns>The assigned tag, or the event id when the event is not recorded.</returns>
    /// <exception cref="ArgumentException">A summary carries an invalid tag.</exception>
    public string Add(EventBase eventToAdd, bool record = true)
    {
        ArgumentNullException.ThrowIfNull(eventToAdd);
        if (AgentContext.CurrentCallId is { } callId && !eventToAdd.Metadata.ContainsKey("call_id"))
        {
            eventToAdd.Metadata["call_id"] = callId;
        }

        if (AgentContext.CurrentEventFormat is { } format && !eventToAdd.Metadata.ContainsKey("truncation_event_format"))
        {
            eventToAdd.Metadata["truncation_event_format"] = format.DeepClone();
        }

        Emit(eventToAdd);
        if (eventToAdd.EventRole == Role.RuntimeEvent || !record)
        {
            return eventToAdd.Id;
        }

        lock (_gate)
        {
            string tag;
            if (eventToAdd is Summary { SummaryTag.Length: > 0 } summary)
            {
                tag = summary.SummaryTag;
                ValidateTag(tag);
            }
            else
            {
                tag = _backend.AllocateNextTag();
            }

            eventToAdd.Tag = tag;
            _backend.Store(tag, eventToAdd);
            return tag;
        }
    }

    /// <summary>Subscribes to events of one type (<see cref="EventBase.EventType"/>, for example <c>Task</c>) or to <c>*</c> for all.</summary>
    /// <returns>A handle. Dispose it to unsubscribe.</returns>
    public IDisposable On(string eventType, Action<EventBase> handler)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (!_handlers.TryGetValue(eventType, out var list))
            {
                _handlers[eventType] = list = [];
            }

            list.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_gate)
            {
                _handlers[eventType].Remove(handler);
            }
        });
    }

    /// <summary>
    /// Registers asynchronous cleanup. <see cref="CloseAsync"/> runs the callbacks in reverse order of registration.
    /// </summary>
    /// <returns>A handle. Dispose it to remove the callback.</returns>
    public IDisposable OnClose(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_gate)
        {
            _closeCallbacks.Add(callback);
        }

        return new Subscription(() =>
        {
            lock (_gate)
            {
                _closeCallbacks.Remove(callback);
            }
        });
    }

    /// <summary>
    /// Runs the registered cleanup callbacks. Concurrent callers share one run. A callback that closes its owner again
    /// returns at once. A failing callback is logged and does not stop the others. It does not close storage.
    /// </summary>
    public Task CloseAsync()
    {
        if (_inDrain.Value)
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            return _closeTask ??= DrainCloseCallbacksAsync();
        }
    }

    /// <summary>Registers middleware for one context type. The first registered middleware is the outermost.</summary>
    /// <returns>A handle. Dispose it to remove the middleware.</returns>
    public IDisposable Intercept<TContext>(Middleware<TContext> middleware)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(middleware);
        lock (_gate)
        {
            if (!_middleware.TryGetValue(typeof(TContext), out var list))
            {
                _middleware[typeof(TContext)] = list = [];
            }

            list.Add(middleware);
        }

        return new Subscription(() =>
        {
            lock (_gate)
            {
                _middleware[typeof(TContext)].Remove(middleware);
            }
        });
    }

    /// <summary>Returns true when middleware is registered for the context type.</summary>
    public bool HasMiddleware<TContext>()
        where TContext : class
    {
        lock (_gate)
        {
            return _middleware.TryGetValue(typeof(TContext), out var list) && list.Count > 0;
        }
    }

    /// <summary>
    /// Runs the middleware chain for the context type and ends with <paramref name="core"/>.
    /// With no middleware, it calls <paramref name="core"/> directly.
    /// </summary>
    /// <exception cref="NooaRuntimeException">A middleware returned null.</exception>
    public Task<TContext> RunMiddlewareAsync<TContext>(TContext context, MiddlewareNext<TContext> core)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(core);
        Delegate[] entries;
        lock (_gate)
        {
            entries = _middleware.TryGetValue(typeof(TContext), out var list) ? [.. list] : [];
        }

        var current = core;
        for (var i = entries.Length - 1; i >= 0; i--)
        {
            current = Layer((Middleware<TContext>)entries[i], current);
        }

        return current(context);
    }

    /// <summary>Returns the event with this tag or id, or null.</summary>
    public EventBase? Get(string key)
    {
        lock (_gate)
        {
            return _backend.Get(key) ?? _backend.GetById(key);
        }
    }

    /// <summary>Returns the event with this tag or id.</summary>
    /// <exception cref="KeyNotFoundException">No event matches.</exception>
    public EventBase this[string key] => Get(key) ?? throw new KeyNotFoundException($"No event with tag or id '{key}'");

    /// <summary>Returns true when a tag or an id exists.</summary>
    public bool Contains(string key) => Get(key) is not null;

    /// <summary>Returns the events that match all given filters, in insertion order, archived events included.</summary>
    /// <param name="type">The <see cref="EventBase.EventType"/> to match.</param>
    /// <param name="callId">The <c>call_id</c> metadata to match.</param>
    /// <param name="executionStatus">The status of a <see cref="CellOutput"/>, in wire form such as <c>error</c> or <c>complete</c>.</param>
    /// <param name="query">Text to find in the public fields. Case-insensitive.</param>
    /// <param name="regex">Treat <paramref name="query"/> as a regular expression. It has a one second match timeout.</param>
    /// <param name="limit">The maximum number of results. The most recent events win.</param>
    public IReadOnlyList<EventBase> Filter(
        string? type = null,
        string? callId = null,
        string? executionStatus = null,
        string? query = null,
        bool regex = false,
        int? limit = null)
    {
        IEnumerable<EventBase> events = AllEvents();
        if (type is not null)
        {
            events = events.Where(e => e.EventType == type);
        }

        if (callId is not null)
        {
            events = events.Where(e => e.Metadata["call_id"]?.GetValue<string>() == callId);
        }

        if (executionStatus is not null)
        {
            events = events.Where(e => e is CellOutput cell && Wire(cell.ExecutionStatus) == executionStatus);
        }

        if (query is not null)
        {
            if (regex)
            {
                var pattern = new Regex(query, RegexOptions.IgnoreCase, _regexTimeout);
                events = events.Where(e => pattern.IsMatch(EventRenderer.SearchableText(e)));
            }
            else
            {
                events = events.Where(e => EventRenderer.SearchableText(e).Contains(query, StringComparison.OrdinalIgnoreCase));
            }
        }

        var result = events.ToList();
        return limit is { } max && result.Count > max ? result[^max..] : result;
    }

    /// <summary>
    /// Sets the run-time event query. It has the highest priority and overrides the query of a scope and of the agent.
    /// Pass null to clear it.
    /// </summary>
    public void SetEventQuery(EventQuery? query)
    {
        lock (_gate)
        {
            _eventQuery = query;
        }
    }

    /// <summary>Returns the run-time event query, or null when none is set.</summary>
    public EventQuery? GetEventQuery()
    {
        lock (_gate)
        {
            return _eventQuery;
        }
    }

    /// <summary>Returns all recorded events in insertion order, archived history included. Use it for export, not for prompts.</summary>
    public IReadOnlyList<EventBase> AllEvents()
    {
        lock (_gate)
        {
            return [.. _backend.AllEvents()];
        }
    }

    /// <summary>Returns the tag and event pairs of the active events in view order.</summary>
    public IReadOnlyList<KeyValuePair<string, EventBase>> Items()
    {
        lock (_gate)
        {
            return
            [
                .. _backend.ActiveTags()
                    .Select(tag => (tag, evt: _backend.Get(tag)))
                    .Where(p => p.evt is not null)
                    .Select(p => KeyValuePair.Create(p.tag, p.evt!)),
            ];
        }
    }

    /// <summary>Returns the active tags in view order. After a collapse: <c>1</c>, <c>2..40</c>, <c>41</c>.</summary>
    public IReadOnlyList<string> Keys()
    {
        lock (_gate)
        {
            return _backend.ActiveTags();
        }
    }

    /// <summary>Returns the active events in view order.</summary>
    public IReadOnlyList<EventBase> Values() => [.. Items().Select(p => p.Value)];

    /// <summary>
    /// Changes an event, found by tag or id, with <paramref name="mutate"/>. It does not notify subscribers.
    /// Set <c>Metadata</c> entries in the delegate to merge metadata.
    /// </summary>
    /// <returns>True when the event was found.</returns>
    public bool Update(string key, Action<EventBase> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (_gate)
        {
            if (_backend.Update(key, mutate))
            {
                return true;
            }

            return _backend.GetById(key) is { } found && _backend.FindTag(found) is { Length: > 0 } tag && _backend.Update(tag, mutate);
        }
    }

    /// <summary>Removes an event by tag or id. Returns true when it was found.</summary>
    public bool Remove(string key)
    {
        lock (_gate)
        {
            if (_backend.Remove(key))
            {
                return true;
            }

            return _backend.GetById(key) is { } found && _backend.FindTag(found) is { Length: > 0 } tag && _backend.Remove(tag);
        }
    }

    /// <summary>Removes all events and resets the tag counter.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _backend.Clear();
        }
    }

    /// <summary>
    /// Archives a range of events and adds a <see cref="Summary"/> in its place. Without text this is truncation.
    /// With text it is summarization. An event inside the range keeps its tag, so <c>this["5"]</c> still works.
    /// </summary>
    /// <param name="startTag">The first tag, inclusive, for example <c>2</c>.</param>
    /// <param name="endTag">The last tag, inclusive, for example <c>40</c>.</param>
    /// <param name="summaryText">The summary, or null for truncation.</param>
    /// <returns>The tag of the new summary, for example <c>2..40</c>.</returns>
    /// <exception cref="ArgumentException">A tag is not numeric, or no active event is in the range.</exception>
    public string Collapse(string startTag, string endTag, string? summaryText = null)
    {
        var startNum = ParseTagStart(startTag);
        var endNum = ParseTagEnd(endTag);
        Summary summary;
        lock (_gate)
        {
            var collapsed = _backend.ActiveTags()
                .Where(t => ParseTagStart(t) <= endNum && ParseTagEnd(t) >= startNum)
                .ToList();
            if (collapsed.Count == 0)
            {
                throw new ArgumentException($"No active events in range '{startTag}'..'{endTag}'");
            }

            foreach (var tag in collapsed)
            {
                _backend.SetStatus(tag, EventStatus.Archived);
                _backend.RemoveActiveTag(tag);
            }

            var actualStart = collapsed.Min(ParseTagStart);
            var actualEnd = collapsed.Max(ParseTagEnd);
            var summaryTag = $"{actualStart}..{actualEnd}";
            var firstStart = ParseTagStart(collapsed[0]);
            var active = _backend.ActiveTags();
            var insertAt = active.Count;
            for (var i = 0; i < active.Count; i++)
            {
                if (ParseTagStart(active[i]) > firstStart)
                {
                    insertAt = i;
                    break;
                }
            }

            summary = new Summary
            {
                SummaryTag = summaryTag,
                ReplacedRange = new SeqRange(actualStart, actualEnd),
                ChildrenTags = collapsed,
                SummaryText = summaryText,
                Doc = "Search original events (including archived): "
                    + "self.Events.Query(query: \"keyword\", limit: 10). "
                    + $"Read by tag, for example self.Events[\"{actualStart}\"]. "
                    + $"Expand this summary through the ChildrenTags of self.Events[\"{summaryTag}\"]. "
                    + "For nested summaries, expand their ChildrenTags again. "
                    + "Inspect originals when exact wording or omitted details matter.",
                Tag = summaryTag,
            };
            _backend.Store(summaryTag, summary);
            _backend.RemoveActiveTag(summaryTag);
            _backend.InsertActiveTag(summaryTag, insertAt);
        }

        Emit(summary);
        return summary.SummaryTag;
    }

    private static MiddlewareNext<TContext> Layer<TContext>(Middleware<TContext> middleware, MiddlewareNext<TContext> next)
        where TContext : class =>
        async context =>
            await middleware(context, next).ConfigureAwait(false)
            ?? throw new NooaRuntimeException(
                "Middleware returned null. Middleware must return the context "
                + "(return await next(context), or set the result and return the context).");

    private static string Wire<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonSerializer.Serialize(value, NooaJson.Options).Trim('"');

    private static int ParseTagStart(string tag) => ParseTag(tag, 0);

    private static int ParseTagEnd(string tag) => ParseTag(tag, 1);

    private static int ParseTag(string tag, int part)
    {
        var pieces = tag.Split("..");
        var text = pieces.Length == 2 ? pieces[part] : tag;
        return pieces.Length <= 2 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : throw new ArgumentException(
                $"Invalid tag format '{tag}': tags must be numeric (for example '5') or ranges (for example '2..40')");
    }

    private static void ValidateTag(string tag)
    {
        var pieces = tag.Split("..");
        if (pieces.Length > 2)
        {
            throw new ArgumentException($"Invalid range tag '{tag}': must have exactly one '..' separator");
        }

        var numbers = new int[pieces.Length];
        for (var i = 0; i < pieces.Length; i++)
        {
            if (!int.TryParse(pieces[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                throw new ArgumentException(
                    pieces.Length == 2
                        ? $"Invalid range tag '{tag}': start and end must be integers"
                        : $"Invalid tag '{tag}': must be a numeric string (for example '5')");
            }
        }

        if (numbers.Length == 2 && numbers[0] > numbers[1])
        {
            throw new ArgumentException($"Invalid range tag '{tag}': start ({numbers[0]}) must be <= end ({numbers[1]})");
        }
    }

    private void Emit(EventBase e)
    {
        Action<EventBase>[] handlers;
        lock (_gate)
        {
            handlers =
            [
                .. _handlers.GetValueOrDefault(e.EventType) ?? [],
                .. _handlers.GetValueOrDefault("*") ?? [],
            ];
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(e);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event handler for {EventType} raised", e.EventType);
            }
        }
    }

    private async Task DrainCloseCallbacksAsync()
    {
        await Task.Yield();
        _inDrain.Value = true;
        try
        {
            Func<Task>[] callbacks;
            lock (_gate)
            {
                callbacks = [.. _closeCallbacks];
                _closeCallbacks.Clear();
            }

            foreach (var callback in callbacks.Reverse())
            {
                try
                {
                    await callback().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background component cleanup failed");
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _closeTask = null;
            }
        }
    }
}
