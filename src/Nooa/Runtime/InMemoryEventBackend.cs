// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/event_backend.py

using Nooa.ContextBlocks;

namespace Nooa.Runtime;

/// <summary>
/// In-memory <see cref="IEventBackend"/>. It keeps live event objects: a dictionary for the tag lookup, a list for the
/// active tags and a list of all events in order. It is the default backend. It is not thread safe.
/// </summary>
public sealed class InMemoryEventBackend : IEventBackend
{
    private readonly List<EventBase> _events = [];
    private readonly Dictionary<string, EventBase> _tagToEvent = [];
    private readonly List<string> _activeTags = [];
    private long _nextTagNum;

    /// <summary>Creates an empty backend. The tag counter starts at 1.</summary>
    public InMemoryEventBackend()
    {
        _nextTagNum = MaxTagNum() + 1;
    }

    /// <inheritdoc />
    public int Count => _events.Count;

    /// <inheritdoc />
    public void Store(string tag, EventBase eventToStore)
    {
        _events.Add(eventToStore);
        _tagToEvent[tag] = eventToStore;
        _activeTags.Add(tag);
        _nextTagNum = Math.Max(_nextTagNum, EventTags.MaxNum(tag) + 1);
    }

    /// <inheritdoc />
    public EventBase? Get(string tag) => _tagToEvent.GetValueOrDefault(tag);

    /// <inheritdoc />
    public EventBase? GetById(string eventId) => _events.FirstOrDefault(e => e.Id == eventId);

    /// <inheritdoc />
    public bool Update(string tag, Action<EventBase> mutate)
    {
        var found = Get(tag);
        if (found is null)
        {
            return false;
        }

        mutate(found);
        return true;
    }

    /// <inheritdoc />
    public bool Remove(string tag)
    {
        if (!_tagToEvent.Remove(tag, out var found))
        {
            return false;
        }

        _activeTags.Remove(tag);
        var index = _events.FindIndex(e => e.Id == found.Id);
        if (index >= 0)
        {
            _events.RemoveAt(index);
        }

        return true;
    }

    /// <inheritdoc />
    public bool SetStatus(string tag, EventStatus status)
    {
        var found = Get(tag);
        if (found is null)
        {
            return false;
        }

        found.Status = status;
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ActiveTags() => [.. _activeTags];

    /// <inheritdoc />
    public void InsertActiveTag(string tag, int index) => _activeTags.Insert(index, tag);

    /// <inheritdoc />
    public bool RemoveActiveTag(string tag) => _activeTags.Remove(tag);

    /// <inheritdoc />
    public IEnumerable<EventBase> AllEvents() => _events.ToList();

    /// <inheritdoc />
    public string? FindTag(EventBase eventToFind)
    {
        foreach (var (tag, stored) in _tagToEvent)
        {
            if (ReferenceEquals(stored, eventToFind) || stored.Id == eventToFind.Id)
            {
                return tag;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _events.Clear();
        _tagToEvent.Clear();
        _activeTags.Clear();
        _nextTagNum = 1;
    }

    /// <summary>The highest tag number in storage, or 0 if there is none.</summary>
    public long MaxTagNum() => _tagToEvent.Count == 0 ? 0 : _tagToEvent.Keys.Max(EventTags.MaxNum);

    /// <inheritdoc />
    public string AllocateNextTag() => (_nextTagNum++).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
