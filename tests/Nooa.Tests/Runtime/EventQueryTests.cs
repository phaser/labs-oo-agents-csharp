// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_event_query.py

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class EventQueryTests
{
    private static T InCall<T>(T evt, string? callId) where T : EventBase
    {
        if (callId is not null)
        {
            evt.Metadata["call_id"] = callId;
        }

        return evt;
    }

    private static List<EventBase> Sample() =>
    [
        InCall(new TaskEvent { Prompt = "first task" }, "call_a"),
        InCall(new Error { Content = "boom" }, "call_a"),
        InCall(new TaskEvent { Prompt = "second task" }, "call_b"),
        new Message { Content = "no call id" },
    ];

    [Fact]
    public void Apply_CallId_FiltersByMetadata()
    {
        var result = new EventQuery { CallId = "call_a" }.Apply(Sample());

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("call_a", e.Metadata["call_id"]!.GetValue<string>()));
    }

    [Fact]
    public void Apply_Current_ResolvesToTheCurrentCallId()
    {
        var result = EventQuery.CurrentCall().Apply(Sample(), currentCallId: "call_b");

        Assert.Equal("second task", Assert.IsType<TaskEvent>(Assert.Single(result)).Prompt);
    }

    [Fact]
    public void Apply_EventsWithoutCallId_AreExcluded() =>
        Assert.DoesNotContain(EventQuery.CurrentCall().Apply(Sample(), "call_a"), e => e is Message);

    [Fact]
    public void Apply_CurrentWithoutACurrentCall_MatchesNothing() => Assert.Empty(EventQuery.CurrentCall().Apply(Sample()));

    [Fact]
    public void Apply_CurrentCall_KeepsTheTaskAndTheMessages()
    {
        var events = new List<EventBase> { InCall(new TaskEvent { Prompt = "t" }, "c"), InCall(new Message { Content = "m" }, "c") };

        Assert.Equal(2, EventQuery.CurrentCall().Apply(events, "c").Count);
    }

    [Fact]
    public void Apply_Type_UsesThePersistedEventTypeName()
    {
        Assert.Equal(2, EventQuery.ByType("Task").Apply(Sample()).Count);
        Assert.Single(EventQuery.ByType("Error").Apply(Sample()));
    }

    [Fact]
    public void Apply_PlainText_IsCaseInsensitiveOverPublicFields()
    {
        var result = new EventQuery { Query = "SECOND" }.Apply(Sample());

        Assert.Equal("second task", Assert.IsType<TaskEvent>(Assert.Single(result)).Prompt);
    }

    [Fact]
    public void Apply_Text_DoesNotSearchHiddenFields()
    {
        var evt = new UserEvent { Content = "x", Metadata = new JsonObject { ["secret"] = "needle" } };

        Assert.Empty(new EventQuery { Query = "needle" }.Apply([evt]));
        Assert.Empty(new EventQuery { Query = evt.Id }.Apply([evt]));
    }

    [Fact]
    public void Apply_Regex_IsCaseSensitive()
    {
        Assert.Equal(2, new EventQuery { Query = "t.sk$", Regex = true }.Apply(Sample()).Count);
        Assert.Empty(new EventQuery { Query = "TASK", Regex = true }.Apply(Sample()));
    }

    [Fact]
    public void Apply_CatastrophicRegex_TimesOutInsteadOfHanging()
    {
        var evt = new UserEvent { Content = new string('a', 60) + "!" };

        Assert.Throws<RegexMatchTimeoutException>(() => new EventQuery { Query = "^(a+)+$", Regex = true }.Apply([evt]));
    }

    [Fact]
    public void Apply_Limit_KeepsTheLastEvents()
    {
        var result = EventQuery.LastN(2).Apply(Sample());

        Assert.Equal(2, result.Count);
        Assert.IsType<Message>(result[^1]);
    }

    [Fact]
    public void Apply_Filters_AreCombinedInOrderBeforeTheLimit()
    {
        var query = new EventQuery { Type = "Task", CallId = "call_a", Limit = 5 };

        Assert.Single(query.Apply(Sample()));
    }

    [Fact]
    public void Apply_NoFilters_ReturnsAllEvents() => Assert.Equal(4, new EventQuery().Apply(Sample()).Count);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Limit_NotPositive_Throws(int limit) => Assert.Throws<ArgumentOutOfRangeException>(() => new EventQuery { Limit = limit });

    [Fact]
    public void Factories_SetTheirFields()
    {
        Assert.Equal(new EventQuery { CallId = "current", Limit = 3 }, EventQuery.CurrentCall(3));
        Assert.Equal(new EventQuery { Type = "Error", Limit = 1 }, EventQuery.ByType("Error", 1));
        Assert.Equal(new EventQuery { Limit = 7 }, EventQuery.LastN(7));
    }
}
