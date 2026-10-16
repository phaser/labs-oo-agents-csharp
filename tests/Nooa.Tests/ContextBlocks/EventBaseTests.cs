// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_events.py

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.ContextBlocks;
using Nooa.Json;
using Nooa.Llm;

namespace Nooa.Tests.ContextBlocks;

public class EventBaseTests
{
    [Fact]
    public void Timestamp_HasDefault()
    {
        var e = new UserEvent { Content = "test" };
        Assert.True(e.Timestamp > DateTimeOffset.Now.AddMinutes(-1));
    }

    [Fact]
    public void Id_IsUniquePerEvent()
    {
        Assert.NotEqual(new UserEvent { Content = "a" }.Id, new UserEvent { Content = "a" }.Id);
        Assert.True(Guid.TryParse(new UserEvent { Content = "a" }.Id, out _));
    }

    [Fact]
    public void Metadata_DefaultsToEmpty_AndIsMutable()
    {
        var e = new UserEvent { Content = "test" };
        Assert.Empty(e.Metadata);

        e.Metadata["custom_key"] = "value";
        e.Metadata["number"] = 42;

        Assert.Equal("value", (string?)e.Metadata["custom_key"]);
        Assert.Equal(42, (int?)e.Metadata["number"]);
    }

    [Fact]
    public void Tag_DefaultsToNull_AndCanBeSet()
    {
        var e = new UserEvent { Content = "test" };
        Assert.Null(e.Tag);

        e.Tag = "42";
        Assert.Equal("42", e.Tag);
    }

    [Fact]
    public void EventType_AndRole_FollowTheEventClass()
    {
        var user = new UserEvent { Content = "t" };
        var assistant = new AssistantEvent { Content = "t" };
        var call = new ToolCallEvent { ToolCallId = "tc1", Name = "test", Arguments = [] };

        Assert.Equal("UserEvent", user.EventType);
        Assert.Equal("AssistantEvent", assistant.EventType);
        Assert.Equal("ToolCallEvent", call.EventType);
        Assert.Equal(Role.User, user.EventRole);
        Assert.Equal(Role.Assistant, assistant.EventRole);
        Assert.Equal(Role.Assistant, call.EventRole);
    }

    [Fact]
    public void IsEmpty_DefaultsToFalse()
    {
        Assert.False(new UserEvent { Content = "" }.IsEmpty);
    }

    [Fact]
    public void Serialize_WritesTheDiscriminatorFirst()
    {
        var json = EventJson.Write(new UserEvent { Content = "Test message" });

        Assert.StartsWith("{\"event_type\":\"UserEvent\"", json);
    }

    [Fact]
    public void Deserialize_UnknownEventType_Throws()
    {
        Assert.Throws<JsonException>(() => EventJson.Read("""{"event_type":"Evil","content":"x"}"""));
    }

    [Fact]
    public void Deserialize_MissingDiscriminator_Throws()
    {
        Assert.Throws<NotSupportedException>(() => EventJson.Read("""{"content":"x"}"""));
    }

    [Fact]
    public void Deserialize_MissingRequiredProperty_Throws()
    {
        Assert.Throws<JsonException>(() => EventJson.Read("""{"event_type":"UserEvent"}"""));
    }

    [Fact]
    public void Allowlist_ListsEveryConcreteEventType_WithAUniqueName()
    {
        var concrete = typeof(EventBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(EventBase)) && !t.IsAbstract)
            .ToHashSet();
        var listed = typeof(EventBase).GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>().ToList();

        Assert.Equal(concrete, listed.Select(a => a.DerivedType).ToHashSet());
        Assert.Equal(listed.Count, listed.Select(a => a.TypeDiscriminator).Distinct().Count());
    }

    [Fact]
    public void EventList_RoundTrips()
    {
        EventBase[] events = [new UserEvent { Content = "Question" }, new AssistantEvent { Content = "Answer" }];

        var restored = JsonSerializer.Deserialize<EventBase[]>(JsonSerializer.Serialize(events, NooaJson.Options), NooaJson.Options)!;

        Assert.Equal(2, restored.Length);
        Assert.Equal("UserEvent", restored[0].EventType);
        Assert.Equal("AssistantEvent", restored[1].EventType);
    }

    [Fact]
    public void RoundTrip_KeepsBaseFields()
    {
        var e = new UserEvent { Content = "hi", Metadata = new JsonObject { ["call_id"] = "abc" }, Tag = "5" };
        e.Status = EventStatus.Archived;

        var back = EventJson.RoundTrip(e);

        Assert.Equal(e.Id, back.Id);
        Assert.Equal(e.Timestamp, back.Timestamp);
        Assert.Equal("5", back.Tag);
        Assert.Equal(EventStatus.Archived, back.Status);
        Assert.Equal("abc", (string?)back.Metadata["call_id"]);
    }

    [Fact]
    public void Serialize_LlmResponseAsBase_UsesUpstreamName()
    {
        Assert.StartsWith("{\"event_type\":\"LLMResponse\"", EventJson.Write(new LlmResponse()));
    }
}
