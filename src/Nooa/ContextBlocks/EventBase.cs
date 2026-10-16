// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py

using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Nooa.Events;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Durable record with identity, lifecycle and public searchable fields.
/// </summary>
/// <remarks>
/// <para>
/// Events are the public conversation IR, not provider messages. The formatter decides how each event type
/// contributes to model context.
/// </para>
/// <para>
/// Persisted types are an explicit allowlist (decision D11). Each type is listed once below, with the
/// <c>event_type</c> discriminator string that upstream uses. Add a new event type to this list.
/// </para>
/// <para>
/// Properties documented as "Not shown to the model" are hidden from the rendered context.
/// </para>
/// </remarks>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "event_type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(UserEvent), "UserEvent")]
[JsonDerivedType(typeof(AssistantEvent), "AssistantEvent")]
[JsonDerivedType(typeof(ToolCallEvent), "ToolCallEvent")]
[JsonDerivedType(typeof(TaskEvent), "Task")]
[JsonDerivedType(typeof(Message), "Message")]
[JsonDerivedType(typeof(Error), "Error")]
[JsonDerivedType(typeof(Feedback), "Feedback")]
[JsonDerivedType(typeof(DebugTrace), "DebugTrace")]
[JsonDerivedType(typeof(TextOnlyReply), "TextOnlyReply")]
[JsonDerivedType(typeof(LoopGuardTriggered), "LoopGuardTriggered")]
[JsonDerivedType(typeof(CellOutput), "CellOutput")]
[JsonDerivedType(typeof(BeforeTurn), "BeforeTurn")]
[JsonDerivedType(typeof(AfterTurn), "AfterTurn")]
[JsonDerivedType(typeof(BeforeAgentCall), "BeforeAgentCall")]
[JsonDerivedType(typeof(AfterAgentCall), "AfterAgentCall")]
[JsonDerivedType(typeof(LlmCallStart), "LLMCallStart")]
[JsonDerivedType(typeof(LlmCallEnd), "LLMCallEnd")]
[JsonDerivedType(typeof(Notification), "Notification")]
[JsonDerivedType(typeof(Summary), "Summary")]
[JsonDerivedType(typeof(SystemPrompt), "SystemPrompt")]
[JsonDerivedType(typeof(LlmResponse), "LLMResponse")]
public abstract record EventBase
{
    private static readonly Lazy<IReadOnlyDictionary<Type, string>> _eventTypeNames = new(() =>
        typeof(EventBase).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => a.DerivedType, a => (string)a.TypeDiscriminator!));

    /// <summary>Creates an event that renders with the given provider role.</summary>
    protected EventBase(Role role)
    {
        EventRole = role;
    }

    /// <summary>The provider role this event renders as.</summary>
    [JsonIgnore]
    public Role EventRole { get; }

    /// <summary>
    /// The persisted <c>event_type</c> discriminator of this event, for example <c>UserEvent</c>.
    /// It is the name that subscribers use to select events.
    /// </summary>
    [JsonIgnore]
    public string EventType => _eventTypeNames.Value[GetType()];

    /// <summary>
    /// Whether this event has nothing to contribute to model context. Events are meaningful by default.
    /// </summary>
    [JsonIgnore]
    public virtual bool IsEmpty => false;

    /// <summary>Unique identifier. Not shown to the model.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Arbitrary metadata, such as call_id or source. Not shown to the model.</summary>
    public JsonObject Metadata { get; init; } = new();

    /// <summary>Active or archived. Not shown to the model.</summary>
    public EventStatus Status { get; set; } = EventStatus.Active;

    /// <summary>Positional tag set by the event manager, such as <c>5</c> or <c>2..40</c>. Not shown to the model.</summary>
    public string? Tag { get; set; }

    /// <summary>Creation time. Not shown to the model.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}
