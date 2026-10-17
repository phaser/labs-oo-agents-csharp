// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_events_api.py

using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Runtime;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Runtime;

public class EventsApiTests
{
    private static (NoteTakingAgent Agent, EventsApi Events) Build(int count = 0)
    {
        var agent = new NoteTakingAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() });
        for (var i = 1; i <= count; i++)
        {
            agent.EventManager.Add(new UserEvent { Content = "event " + i });
        }

        return (agent, agent.Events);
    }

    [Fact]
    public void Query_ByTypeAndText_ReturnsMatchesInOrder()
    {
        var (agent, events) = Build();
        agent.EventManager.Add(new UserEvent { Content = "order 42" });
        agent.EventManager.Add(new TaskEvent { Prompt = "summarise" });
        agent.EventManager.Add(new UserEvent { Content = "order 43" });

        Assert.Equal(2, events.Query(type: "UserEvent").Count);
        Assert.Equal(["order 42", "order 43"], events.Query(type: "UserEvent", query: "ORDER").Select(e => ((UserEvent)e).Content));
        Assert.Single(events.Query(type: "Task"));
        Assert.Single(events.Query(query: @"order 4[3]", regex: true));
    }

    [Fact]
    public void Query_Limit_KeepsTheMostRecent()
    {
        var (_, events) = Build(5);

        Assert.Equal(["event 4", "event 5"], events.Query(limit: 2).Select(e => ((UserEvent)e).Content));
    }

    [Fact]
    public void Query_NoLimit_IsCappedAtMaxResults()
    {
        var (_, events) = Build(EventsApi.MaxResults + 5);

        Assert.Equal(EventsApi.MaxResults, events.Query().Count);
        Assert.Equal(EventsApi.MaxResults, events.Query(limit: int.MaxValue).Count);
        Assert.Equal("event " + (EventsApi.MaxResults + 5), ((UserEvent)events.Query()[^1]).Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Query_LimitBelowOne_Throws(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Build().Events.Query(limit: limit));

    [Fact]
    public void Query_OversizedInput_Throws()
    {
        var (_, events) = Build(1);
        var longKey = new string('a', EventsApi.MaxKeyLength + 1);

        Assert.Throws<ArgumentException>(() => events.Query(type: longKey));
        Assert.Throws<ArgumentException>(() => events.Query(callId: longKey));
        Assert.Throws<ArgumentException>(() => events.Query(executionStatus: longKey));
        Assert.Throws<ArgumentException>(() => events.Query(query: new string('a', EventsApi.MaxQueryLength + 1)));
        Assert.Throws<ArgumentException>(() => events.Query(query: new string('a', EventsApi.MaxRegexLength + 1), regex: true));
        Assert.Empty(events.Query(query: new string('a', EventsApi.MaxQueryLength)));
    }

    [Fact]
    public void Query_CatastrophicRegex_FinishesQuickly()
    {
        var (agent, events) = Build();
        agent.EventManager.Add(new UserEvent { Content = new string('a', 5000) + "!" });

        var started = DateTime.UtcNow;
        var result = events.Query(query: "^(a+)+$", regex: true);

        Assert.Empty(result);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData("(")]
    [InlineData("(?<=a)b")]
    [InlineData(@"(a)\1")]
    public void Query_InvalidOrUnsupportedRegex_ThrowsArgumentException(string pattern) =>
        Assert.ThrowsAny<ArgumentException>(() => Build(1).Events.Query(query: pattern, regex: true));

    [Fact]
    public void Get_ByTagIdAndList_FiltersMissing()
    {
        var (_, events) = Build(3);
        var id = events["2"].Id;

        Assert.Equal("event 2", ((UserEvent)events.Get("2")!).Content);
        Assert.Equal(id, events.Get(id)!.Id);
        Assert.Null(events.Get("9"));
        Assert.Equal(2, events.Get(["1", "9", "3"]).Count);
        Assert.True(events.Contains("3"));
        Assert.False(events.Contains("4"));
    }

    [Fact]
    public void Indexer_MissingKey_Throws()
    {
        var (_, events) = Build(2);

        Assert.Equal("event 1", ((UserEvent)events["1"]).Content);
        Assert.Equal(2, events[["1", "2"]].Count);
        Assert.Throws<KeyNotFoundException>(() => events["7"]);
        Assert.Throws<KeyNotFoundException>(() => events[["1", "7"]]);
    }

    [Fact]
    public void Get_OversizedKeyOrList_Throws()
    {
        var (_, events) = Build(1);

        Assert.Throws<ArgumentException>(() => events.Get(new string('1', EventsApi.MaxKeyLength + 1)));
        Assert.Throws<ArgumentException>(() => events.Get([.. Enumerable.Repeat("1", EventsApi.MaxResults + 1)]));
        Assert.Throws<ArgumentException>(() => events[[.. Enumerable.Repeat("1", EventsApi.MaxResults + 1)]]);
    }

    [Fact]
    public void Collapse_ReplacesTheRangeWithASummary_AndKeepsOriginalsReadable()
    {
        var (_, events) = Build(5);

        var tag = events.Collapse("2", "4", "middle");

        Assert.Equal("2..4", tag);
        Assert.Equal(["1", "2..4", "5"], events.Keys());
        var summary = Assert.IsType<Summary>(events[tag]);
        Assert.Equal("middle", summary.SummaryText);
        Assert.Equal(3, events.Get(summary.ChildrenTags).Count);
        Assert.Equal("event 3", ((UserEvent)events["3"]).Content);
    }

    [Fact]
    public void Collapse_Integers_MustIdentifyExistingEvents()
    {
        var (_, events) = Build(4);

        Assert.Equal("1..2", events.Collapse(1, 2));
        Assert.Throws<ArgumentException>(() => events.Collapse(1, 99));
        Assert.Throws<ArgumentException>(() => events.Collapse(0, 2));
    }

    [Fact]
    public void Collapse_OversizedInput_Throws()
    {
        var (_, events) = Build(2);

        Assert.Throws<ArgumentException>(() => events.Collapse("1", "2", new string('x', EventsApi.MaxSummaryLength + 1)));
        Assert.Throws<ArgumentException>(() => events.Collapse(new string('1', EventsApi.MaxKeyLength + 1), "2"));
        Assert.Equal("1..2", events.Collapse("1", "2", new string('x', EventsApi.MaxSummaryLength)));
    }

    [Fact]
    public void ToString_ShowsTheActiveCount() => Assert.Equal("<EventsApi(3 active)>", Build(3).Events.ToString());
}
