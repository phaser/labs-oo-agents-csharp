// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_event_manager.py, tests/test_event_filtering.py, tests/test_event_middleware.py, tests/test_session_resume_tag_collision.py

using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Runtime;
using Nooa.Storage;

namespace Nooa.Tests.Runtime;

public class EventManagerTests
{
    private static UserEvent User(string text) => new() { Content = text };

    private static EventManager WithEvents(int count)
    {
        var manager = new EventManager();
        for (var i = 1; i <= count; i++)
        {
            manager.Add(User("event " + i));
        }

        return manager;
    }

    [Fact]
    public void Add_AssignsSequentialTags_AndSetsTagOnEvent()
    {
        var manager = new EventManager();
        var first = User("a");

        Assert.Equal("1", manager.Add(first));
        Assert.Equal("2", manager.Add(User("b")));
        Assert.Equal("1", first.Tag);
        Assert.Equal(2, manager.Count);
    }

    [Fact]
    public void Add_NotRecorded_NotifiesButDoesNotStore()
    {
        var manager = new EventManager();
        var seen = new List<EventBase>();
        manager.On("UserEvent", seen.Add);
        var e = User("a");

        var key = manager.Add(e, record: false);

        Assert.Equal(e.Id, key);
        Assert.Single(seen);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Add_RuntimeEvent_IsNeverRecorded()
    {
        var manager = new EventManager();

        manager.Add(new BeforeTurn { MethodName = "m", Strategy = "s", GenerationId = "g", TurnNumber = 1 });

        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Add_SummaryWithTag_UsesItAndValidates()
    {
        var manager = new EventManager();
        var summary = new Summary { SummaryTag = "2..9", ReplacedRange = new SeqRange(2, 9) };

        Assert.Equal("2..9", manager.Add(summary));
        Assert.Throws<ArgumentException>(
            () => manager.Add(new Summary { SummaryTag = "9..2", ReplacedRange = new SeqRange(2, 9) }));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1..2..3")]
    [InlineData("a..b")]
    [InlineData("9..2")]
    [InlineData("-1")]
    public void Add_SummaryWithInvalidTag_Throws(string tag) =>
        Assert.Throws<ArgumentException>(
            () => new EventManager().Add(new Summary { SummaryTag = tag, ReplacedRange = new SeqRange(1, 2) }));

    [Fact]
    public void Get_ByTagAndById()
    {
        var manager = new EventManager();
        var e = User("a");
        manager.Add(e);

        Assert.Same(e, manager.Get("1"));
        Assert.Same(e, manager.Get(e.Id));
        Assert.Same(e, manager[e.Id]);
        Assert.Null(manager.Get("99"));
        Assert.Throws<KeyNotFoundException>(() => manager["99"]);
    }

    [Fact]
    public void Contains_TagIdAndRemoved()
    {
        var manager = new EventManager();
        var e = User("a");
        manager.Add(e);

        Assert.True(manager.Contains("1"));
        Assert.True(manager.Contains(e.Id));
        manager.Remove("1");
        Assert.False(manager.Contains("1"));
    }

    [Fact]
    public void Update_ByTagAndId_AndMissing()
    {
        var manager = new EventManager();
        var e = User("a");
        manager.Add(e);

        Assert.True(manager.Update("1", x => x.Status = EventStatus.Archived));
        Assert.Equal(EventStatus.Archived, e.Status);
        Assert.True(manager.Update(e.Id, x => x.Metadata["k"] = "v"));
        Assert.Equal("v", e.Metadata["k"]!.GetValue<string>());
        Assert.False(manager.Update("99", _ => { }));
    }

    [Fact]
    public void Update_DoesNotNotifySubscribers()
    {
        var manager = WithEvents(1);
        var count = 0;
        manager.On("*", _ => count++);

        manager.Update("1", x => x.Metadata["k"] = 1);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Remove_ByTagAndId_AndMissing()
    {
        var manager = new EventManager();
        var a = User("a");
        manager.Add(a);
        manager.Add(User("b"));

        Assert.True(manager.Remove(a.Id));
        Assert.False(manager.Remove("1"));
        Assert.True(manager.Remove("2"));
        Assert.Empty(manager.Keys());
    }

    [Fact]
    public void Clear_ResetsTags()
    {
        var manager = WithEvents(3);

        manager.Clear();

        Assert.Equal(0, manager.Count);
        Assert.Equal("1", manager.Add(User("again")));
    }

    [Fact]
    public void KeysItemsValues_FollowViewOrder()
    {
        var manager = WithEvents(3);
        manager.Remove("2");

        Assert.Equal(["1", "3"], manager.Keys());
        Assert.Equal(["1", "3"], manager.Items().Select(p => p.Key));
        Assert.Equal(["event 1", "event 3"], manager.Values().Cast<UserEvent>().Select(e => e.Content));
    }

    [Fact]
    public void Keys_Empty() => Assert.Empty(new EventManager().Keys());

    [Fact]
    public void Collapse_ArchivesRangeAndAddsSummary()
    {
        var manager = WithEvents(5);

        var tag = manager.Collapse("2", "4", "summary text");

        Assert.Equal("2..4", tag);
        Assert.Equal(["1", "2..4", "5"], manager.Keys());
        var summary = Assert.IsType<Summary>(manager["2..4"]);
        Assert.Equal("summary text", summary.SummaryText);
        Assert.Equal(["2", "3", "4"], summary.ChildrenTags);
        Assert.Equal(new SeqRange(2, 4), summary.ReplacedRange);
        Assert.Equal(EventStatus.Archived, manager["3"].Status);
        Assert.Equal(5, manager.Filter().Count - 1);
    }

    [Fact]
    public void Collapse_WithoutText_IsTruncation()
    {
        var manager = WithEvents(3);

        manager.Collapse("1", "2");

        Assert.Null(Assert.IsType<Summary>(manager["1..2"]).SummaryText);
    }

    [Fact]
    public void Collapse_Nested_FlattensRange()
    {
        var manager = WithEvents(6);
        manager.Collapse("2", "3");

        var tag = manager.Collapse("2", "5");

        Assert.Equal("2..5", tag);
        Assert.Equal(["1", "2..5", "6"], manager.Keys());
    }

    [Fact]
    public void Collapse_EmptyRange_Throws() =>
        Assert.Throws<ArgumentException>(() => WithEvents(2).Collapse("5", "9"));

    [Fact]
    public void Collapse_InvalidTag_Throws() =>
        Assert.Throws<ArgumentException>(() => WithEvents(2).Collapse("a", "2"));

    [Fact]
    public void Collapse_NotifiesSummarySubscribers()
    {
        var manager = WithEvents(3);
        var seen = new List<EventBase>();
        manager.On("Summary", seen.Add);

        manager.Collapse("1", "3");

        Assert.IsType<Summary>(Assert.Single(seen));
    }

    [Fact]
    public void Collapse_ThenAdd_ContinuesTagProgression()
    {
        var manager = WithEvents(5);
        manager.Collapse("2", "4");

        Assert.Equal("6", manager.Add(User("next")));
    }

    [Fact]
    public void Filter_ByTypeQueryLimit()
    {
        var manager = new EventManager();
        manager.Add(User("apple pie"));
        manager.Add(new Message { Content = "banana" });
        manager.Add(User("Apple tart"));

        Assert.Equal(2, manager.Filter(type: "UserEvent").Count);
        Assert.Equal(2, manager.Filter(query: "APPLE").Count);
        Assert.Single(manager.Filter(query: "APPLE", limit: 1));
        Assert.Equal("Apple tart", Assert.IsType<UserEvent>(manager.Filter(query: "apple", limit: 1)[0]).Content);
        Assert.Empty(manager.Filter(query: "zzz"));
        Assert.Equal(3, manager.Filter(limit: 10).Count);
    }

    [Fact]
    public void Filter_Regex_MatchesAndRejectsInvalidPattern()
    {
        var manager = new EventManager();
        manager.Add(User("order 42"));
        manager.Add(User("order x"));

        Assert.Single(manager.Filter(query: @"order \d+", regex: true));
        Assert.ThrowsAny<ArgumentException>(() => manager.Filter(query: "(", regex: true));
    }

    [Fact]
    public void Filter_SearchesFieldsNotMetadata()
    {
        var manager = new EventManager();
        var e = User("plain");
        e.Metadata["secret"] = "needle";
        manager.Add(e);

        Assert.Empty(manager.Filter(query: "needle"));
    }

    [Fact]
    public void Filter_ByExecutionStatus()
    {
        var manager = new EventManager();
        manager.Add(new CellOutput { ToolCallId = "t", ExecutionStatus = ResultStatus.Error, ExecutionCount = 1 });
        manager.Add(new CellOutput { ToolCallId = "t", ExecutionStatus = ResultStatus.Complete, ExecutionCount = 2 });

        Assert.Single(manager.Filter(executionStatus: "error"));
        Assert.Single(manager.Filter(executionStatus: "complete"));
    }

    [Fact]
    public void Filter_ByCallId_IncludesArchived()
    {
        var manager = new EventManager();
        var a = User("a");
        a.Metadata["call_id"] = "c1";
        var b = User("b");
        b.Metadata["call_id"] = "c2";
        manager.Add(a);
        manager.Add(b);
        manager.Collapse("1", "1");

        Assert.Single(manager.Filter(callId: "c1"));
        Assert.Single(manager.Filter(callId: "c2", type: "UserEvent"));
    }

    [Fact]
    public void On_Handlers_ByTypeAndWildcard_AndUnsubscribeIsIdempotent()
    {
        var manager = new EventManager();
        var typed = 0;
        var all = 0;
        var subscription = manager.On("UserEvent", _ => typed++);
        manager.On("*", _ => all++);

        manager.Add(User("a"));
        manager.Add(new Message { Content = "m" });
        subscription.Dispose();
        subscription.Dispose();
        manager.Add(User("b"));

        Assert.Equal(1, typed);
        Assert.Equal(3, all);
    }

    [Fact]
    public void On_HandlerThatThrows_DoesNotSkipOthers()
    {
        var manager = new EventManager();
        var reached = false;
        manager.On("UserEvent", _ => throw new InvalidOperationException("bad"));
        manager.On("UserEvent", _ => reached = true);

        manager.Add(User("a"));

        Assert.True(reached);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void On_HandlerCanUnsubscribeAndSubscribeDuringEmit()
    {
        var manager = new EventManager();
        IDisposable? self = null;
        var calls = 0;
        var late = 0;
        self = manager.On("UserEvent", _ =>
        {
            calls++;
            self!.Dispose();
            manager.On("UserEvent", _ => late++);
        });

        manager.Add(User("a"));
        manager.Add(User("b"));

        Assert.Equal(1, calls);
        Assert.Equal(1, late);
    }

    [Fact]
    public void Add_TagsEventWithCurrentCallId_AndKeepsExistingOne()
    {
        var agent = new EchoAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() });
        var seen = new List<EventBase>();
        agent.EventManager.On("UserEvent", seen.Add);
        var kept = User("kept");
        kept.Metadata["call_id"] = "mine";

        agent.EventManager.Add(User("outside"));
        agent.EventManager.Add(kept);

        Assert.False(seen[0].Metadata.ContainsKey("call_id"));
        Assert.Equal("mine", seen[1].Metadata["call_id"]!.GetValue<string>());
    }

    [Fact]
    public void SetBackend_KeepsSubscribers_AndUsesNewBackendTags()
    {
        var manager = WithEvents(3);
        var count = 0;
        manager.On("UserEvent", _ => count++);

        manager.SetBackend(new InMemoryEventBackend());

        Assert.Equal(0, manager.Count);
        Assert.Equal("1", manager.Add(User("fresh")));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Constructor_PrepopulatedBackend_ContinuesAfterMaxTag()
    {
        var backend = new InMemoryEventBackend();
        backend.Store("7", User("old"));

        Assert.Equal("8", new EventManager(backend).Add(User("new")));
        Assert.Equal("1", new EventManager(new InMemoryEventBackend()).Add(User("first")));
    }

    [Fact]
    public void TwoManagersOnOneBackend_NeverCollide()
    {
        var backend = new InMemoryEventBackend();
        var one = new EventManager(backend);
        var two = new EventManager(backend);

        var tags = new[] { one.Add(User("a")), two.Add(User("b")), one.Add(User("c")), two.Add(User("d")) };

        Assert.Equal(4, tags.Distinct().Count());
        Assert.Equal(4, backend.Count);
    }

    [Fact]
    public void TwoManagersOnOneSqliteBackend_NeverCollide()
    {
        using var storage = new SqliteStorageManager();
        var one = new EventManager(storage.EventBackend);
        var two = new EventManager(storage.EventBackend);

        var tags = new[] { one.Add(User("a")), two.Add(User("b")), one.Add(User("c")) };

        Assert.Equal(3, tags.Distinct().Count());
    }

    [Fact]
    public void SqliteResume_ContinuesAfterRangeTags()
    {
        using var dir = new TempDirectory();
        var path = dir.File("s.db");
        using (var storage = new SqliteStorageManager(path))
        {
            var manager = new EventManager(storage.EventBackend);
            for (var i = 0; i < 12; i++)
            {
                manager.Add(User("e" + i));
            }

            manager.Collapse("2", "10");
        }

        using var reopened = new SqliteStorageManager(path);

        Assert.Equal("13", new EventManager(reopened.EventBackend).Add(User("resumed")));
    }

    [Fact]
    public async Task Add_ParallelTasks_AllRecordedWithUniqueTags()
    {
        var manager = new EventManager();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => manager.Add(User("e" + i)), TestContext.Current.CancellationToken)));

        Assert.Equal(50, manager.Count);
        Assert.Equal(50, manager.Keys().Distinct().Count());
    }

    [Fact]
    public async Task Middleware_FirstRegisteredIsOutermost()
    {
        var manager = new EventManager();
        var log = new List<string>();
        manager.Intercept<AgentCallContext>(async (c, next) =>
        {
            log.Add("a-in");
            c = await next(c);
            log.Add("a-out");
            return c;
        });
        manager.Intercept<AgentCallContext>(async (c, next) =>
        {
            log.Add("b-in");
            c = await next(c);
            log.Add("b-out");
            return c;
        });

        await manager.RunMiddlewareAsync(Context(manager), c =>
        {
            log.Add("core");
            return Task.FromResult(c);
        });

        Assert.Equal(["a-in", "b-in", "core", "b-out", "a-out"], log);
    }

    [Fact]
    public async Task Middleware_Unsubscribe_RemovesIt()
    {
        var manager = new EventManager();
        var ran = false;
        var subscription = manager.Intercept<AgentCallContext>((c, next) =>
        {
            ran = true;
            return next(c);
        });
        subscription.Dispose();

        await manager.RunMiddlewareAsync(Context(manager), Task.FromResult);

        Assert.False(ran);
        Assert.False(manager.HasMiddleware<AgentCallContext>());
    }

    [Fact]
    public async Task Middleware_ReturningNull_Throws()
    {
        var manager = new EventManager();
        manager.Intercept<AgentCallContext>((_, _) => Task.FromResult<AgentCallContext>(null!));

        var e = await Assert.ThrowsAsync<NooaRuntimeException>(() => manager.RunMiddlewareAsync(Context(manager), Task.FromResult));

        Assert.Contains("returned null", e.Message);
    }

    [Fact]
    public async Task Middleware_KindsAreSeparate()
    {
        var manager = new EventManager();
        manager.Intercept<ExecuteCodeContext>((c, next) => next(c));

        Assert.True(manager.HasMiddleware<ExecuteCodeContext>());
        Assert.False(manager.HasMiddleware<AgentCallContext>());
        Assert.False(manager.HasMiddleware<LlmCallContext>());
        Assert.False(manager.HasMiddleware<DecisionCallContext>());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CloseAsync_RunsCallbacksInReverseOrder_AndLogsFailures()
    {
        var manager = new EventManager();
        var order = new List<int>();
        manager.OnClose(() =>
        {
            order.Add(1);
            return Task.CompletedTask;
        });
        manager.OnClose(() => throw new InvalidOperationException("boom"));
        manager.OnClose(() =>
        {
            order.Add(3);
            return Task.CompletedTask;
        });

        await manager.CloseAsync();

        Assert.Equal([3, 1], order);
    }

    [Fact]
    public async Task CloseAsync_CallbackThatClosesOwner_DoesNotDeadlock()
    {
        var manager = new EventManager();
        manager.OnClose(() => manager.CloseAsync());

        await manager.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CloseAsync_ConcurrentCallers_ShareOneRun()
    {
        var manager = new EventManager();
        var runs = 0;
        manager.OnClose(async () =>
        {
            Interlocked.Increment(ref runs);
            await Task.Delay(30, TestContext.Current.CancellationToken);
        });

        await Task.WhenAll(manager.CloseAsync(), manager.CloseAsync());

        Assert.Equal(1, runs);
    }

    private static AgentCallContext Context(EventManager manager) =>
        new() { Agent = new EchoAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() }), MethodName = "m", Arguments = [] };
}
