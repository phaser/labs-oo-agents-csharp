// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/event_backend.py

using Nooa.ContextBlocks;

namespace Nooa.Runtime;

/// <summary>
/// Storage backend for events. Implementations can keep events in memory, in SQLite, in files or elsewhere.
/// </summary>
/// <remarks>
/// A backend stores events with their assigned tags, keeps the tag to event mapping, tracks active and archived
/// status, and keeps insertion order for the active tags. Each implementation is responsible for its own thread safety.
/// A backend that serializes events can only restore the types on the <see cref="EventBase"/> allowlist.
/// </remarks>
public interface IEventBackend
{
    /// <summary>Total number of stored events, active and archived.</summary>
    int Count { get; }

    /// <summary>Stores an event with its tag. The event is stored as active.</summary>
    void Store(string tag, EventBase eventToStore);

    /// <summary>Returns the event for a tag, active or archived, or null if there is none.</summary>
    EventBase? Get(string tag);

    /// <summary>Returns the event with the given id, or null if there is none. Use it when the tag is unknown.</summary>
    EventBase? GetById(string eventId);

    /// <summary>
    /// Changes the event with the given tag by running <paramref name="mutate"/> on it.
    /// Returns true if the event was found and updated, false if not.
    /// </summary>
    /// <remarks>
    /// Python takes keyword fields. Events are records with init-only properties, so the caller sets the mutable members
    /// (<c>Status</c>, <c>Tag</c>, <c>Metadata</c>, <c>ToolCallEvent.Result</c>) in the delegate.
    /// A backend that serializes events saves the event after the delegate returns.
    /// </remarks>
    bool Update(string tag, Action<EventBase> mutate);

    /// <summary>Removes the event from storage. Returns true if it was found.</summary>
    bool Remove(string tag);

    /// <summary>Sets the status of the event, for example to archive it when a range collapses. Returns true if it was found.</summary>
    bool SetStatus(string tag, EventStatus status);

    /// <summary>Returns the active (not archived) tags in insertion order. The order is the rendering order.</summary>
    IReadOnlyList<string> ActiveTags();

    /// <summary>Inserts a tag into the active list at a position, for example for a summary that replaces a range.</summary>
    void InsertActiveTag(string tag, int index);

    /// <summary>Removes a tag from the active list without removing the event. Returns true if it was found.</summary>
    bool RemoveActiveTag(string tag);

    /// <summary>Returns all events in insertion order, active and archived.</summary>
    IEnumerable<EventBase> AllEvents();

    /// <summary>Finds the tag of an event by identity or id. Returns null if the event is not stored.</summary>
    string? FindTag(EventBase eventToFind);

    /// <summary>Removes all events and resets the tag counter.</summary>
    void Clear();

    /// <summary>
    /// Allocates the next sequential tag. The counter lives on the backend so that several event managers on the same
    /// backend never hand out the same tag. A call to <see cref="Store"/> is expected to follow.
    /// </summary>
    string AllocateNextTag();
}
