// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_context_builder.py, tests/test_archived_events_excluded_from_context.py

using Microsoft.Extensions.Logging;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

public class ContextBuilderTests
{
    private sealed class OverridingStrategy(
        IReadOnlyDictionary<string, object?>? overrides = null,
        IReadOnlySet<string>? staticKeys = null,
        IReadOnlyList<string>? order = null) : GenerationStrategy
    {
        public override IReadOnlyDictionary<string, object?>? GetBlockOverrides() => overrides;

        public override IReadOnlySet<string>? GetStaticBlockKeys() => staticKeys;

        public override IReadOnlyList<string>? GetBlockOrder() => order;

        public override Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken) =>
            Task.FromResult<object?>(null);
    }

    private static ContextManager Manager()
    {
        var manager = new ContextManager();
        manager.SetStaticProtected("system_prompt", () => "be helpful");
        manager.SetDynamicProtected("state", () => "state text");
        return manager;
    }

    private static BuildResult Build(
        ContextManager manager,
        EventManager? events = null,
        GenerationStrategy? strategy = null,
        FormatConfig? format = null,
        IReadOnlyDictionary<string, object?>? scoped = null,
        EventQuery? runtimeQuery = null,
        EventQuery? scopedQuery = null,
        EventQuery? agentQuery = null,
        string? callId = null,
        ILogger? logger = null) =>
        ContextBuilder.Build(manager, events ?? new EventManager(), strategy, format, scoped, runtimeQuery, scopedQuery, agentQuery, callId, logger);

    private static ResolvedBlock Find(BuildResult result, string key) => result.Blocks.Single(b => b.Key == key);

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] items) => items.ToDictionary(i => i.Key, i => i.Value);

    [Fact]
    public void Build_PersistentBlocks_KeepInsertionOrderAndFlags()
    {
        var manager = Manager();
        manager.SetStatic("rules", "r");
        manager.Set("notes", "n");

        var result = Build(manager);

        Assert.Equal(["system_prompt", "state", "rules", "notes"], result.Blocks.Select(b => b.Key));
        Assert.Equal(new BlockMetadata(Static: true), Find(result, "system_prompt").Metadata);
        Assert.Equal(new BlockMetadata(), Find(result, "state").Metadata);
        Assert.Equal(new BlockMetadata(UserBlock: true, Static: true), Find(result, "rules").Metadata);
        Assert.Equal(new BlockMetadata(UserBlock: true), Find(result, "notes").Metadata);
    }

    [Fact]
    public void Build_ProviderBlock_IsResolvedAndCached()
    {
        var manager = Manager();
        manager.SetDynamicProvider("clock", () => "12:00");

        var result = Build(manager);

        Assert.Equal("12:00", Find(result, "clock").Content);
        Assert.Equal("12:00", result.ResolvedCache["clock"]);
        Assert.Equal("state text", result.ResolvedCache["state"]);
    }

    [Fact]
    public void Build_ProviderThatThrows_ShowsTheErrorInline_AndWarns()
    {
        var manager = Manager();
        manager.SetDynamicProvider("bad", () => throw new InvalidOperationException("no data"));
        manager.SetDynamicProvider("good", () => "fine");
        var logger = new Nooa.Tests.CapturingLogger();

        var result = Build(manager, logger: logger);

        Assert.Equal("InvalidOperationException: no data", Find(result, "bad").Content);
        Assert.Equal("fine", Find(result, "good").Content);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void Build_ProviderCancellation_Propagates()
    {
        var manager = Manager();
        manager.SetDynamicProvider("slow", () => throw new OperationCanceledException());

        Assert.Throws<OperationCanceledException>(() => Build(manager));
    }

    [Fact]
    public void Build_NullAndNonStringValues_AreFormatted()
    {
        var manager = Manager();
        manager.SetDynamic("count", 5);
        manager.SetDynamic("items", new List<string> { "a", "b" });
        manager.SetDynamicProvider("none", () => null);

        var result = Build(manager);

        Assert.Equal("5", Find(result, "count").Content);
        Assert.Equal("[\"a\", \"b\"]", Find(result, "items").Content);
        Assert.Equal("null", Find(result, "none").Content);
    }

    [Fact]
    public void Build_UsesTheContextBlockFormatBounds()
    {
        var manager = Manager();
        manager.SetDynamic("long", new List<int>(Enumerable.Range(0, 100)));
        manager.SetDynamic("text", new string('x', 100));

        var result = Build(manager, format: new FormatConfig { MaxLength = 4, MaxString = 10 });

        Assert.Contains("len=100", Find(result, "long").Content);
        Assert.Contains("len=100", Find(result, "text").Content);
    }

    [Fact]
    public void Build_StringProviderResult_IsNotTruncated()
    {
        var manager = Manager();
        manager.SetDynamicProvider("doc", () => new string('x', 100));

        Assert.Equal(100, Find(Build(manager, format: new FormatConfig { MaxString = 10 }), "doc").Content.Length);
    }

    [Fact]
    public void Build_DisabledBlock_IsLeftOut()
    {
        var manager = Manager();
        manager.Disable("state");

        Assert.DoesNotContain(Build(manager).Blocks, b => b.Key == "state");
    }

    [Fact]
    public void Build_StrategyOverrides_ReplaceKeepingThePartition_AppendAndRemove()
    {
        var manager = Manager();
        manager.SetDynamic("notes", "old");
        var strategy = new OverridingStrategy(Map(("system_prompt", "overridden"), ("extra", "added"), ("notes", null)));

        var result = Build(manager, strategy: strategy);

        Assert.Equal("overridden", Find(result, "system_prompt").Content);
        Assert.True(Find(result, "system_prompt").Metadata.Static);
        Assert.DoesNotContain(result.Blocks, b => b.Key == "notes");
        Assert.Equal("added", Find(result, "extra").Content);
        Assert.True(Find(result, "extra").Metadata.Static);
    }

    [Fact]
    public void Build_StrategyOverrideWithProvider_IsDynamicByDefault_AndStaticKeysApply()
    {
        var strategy = new OverridingStrategy(
            Map(("live", new DynamicContext(() => "now")), ("listed", new DynamicContext(() => "x"))),
            new HashSet<string> { "listed" });

        var result = Build(Manager(), strategy: strategy);

        Assert.False(Find(result, "live").Metadata.Static);
        Assert.True(Find(result, "listed").Metadata.Static);
        Assert.Equal("now", Find(result, "live").Content);
    }

    [Fact]
    public void Build_ContextOverride_SetsThePartition()
    {
        var scoped = Map(("a", Context.Of("1", prefix: true)), ("b", Context.FromProvider(() => "2")), ("c", Context.FromProvider(() => "3", prefix: true)));

        var result = Build(Manager(), scoped: scoped);

        Assert.True(Find(result, "a").Metadata.Static);
        Assert.False(Find(result, "b").Metadata.Static);
        Assert.True(Find(result, "c").Metadata.Static);
        Assert.Equal("2", Find(result, "b").Content);
    }

    [Fact]
    public void Build_ScopedOverride_BeatsTheStrategyOverride()
    {
        var strategy = new OverridingStrategy(Map(("focus", "strategy")));

        var result = Build(Manager(), strategy: strategy, scoped: Map(("focus", "scope")));

        Assert.Equal("scope", Find(result, "focus").Content);
    }

    [Fact]
    public void Build_DisabledKey_IsNotAddedByAnOverride()
    {
        var manager = Manager();
        manager.Disable("state");

        var result = Build(manager, scoped: Map(("state", "forced")));

        Assert.DoesNotContain(result.Blocks, b => b.Key == "state");
    }

    [Fact]
    public void Build_BlockOrder_PutsListedKeysFirst()
    {
        var manager = Manager();
        manager.SetStatic("zeta", "z");
        manager.SetStatic("alpha", "a");

        var result = Build(manager, strategy: new OverridingStrategy(order: ["zeta", "system_prompt"]));

        Assert.Equal(["zeta", "system_prompt", "state", "alpha"], result.Blocks.Select(b => b.Key));
    }

    [Fact]
    public void Build_DoesNotChangeTheContextManager()
    {
        var manager = Manager();
        manager.SetDynamicProvider("clock", () => "12:00");

        Build(manager);

        Assert.Throws<Nooa.Errors.DynamicNotResolvedException>(() => manager.Get("clock"));
    }

    private static EventManager EventsOf(params EventBase[] events)
    {
        var manager = new EventManager();
        foreach (var evt in events)
        {
            manager.Add(evt);
        }

        return manager;
    }

    [Fact]
    public void Build_Events_AreBlocksWithRolesAndTags()
    {
        var events = EventsOf(new TaskEvent { Prompt = "go" }, new Message { Content = "ok" });

        var result = Build(Manager(), events);
        var eventBlocks = result.Blocks.Where(b => b.Event is not null).ToList();

        Assert.Equal(["event_1", "event_2"], eventBlocks.Select(b => b.Key));
        Assert.Equal([Role.User, Role.Assistant], eventBlocks.Select(b => b.Role));
        Assert.Equal(["1", "2"], eventBlocks.Select(b => b.Metadata.Tag));
        Assert.All(eventBlocks, b => Assert.Equal("", b.Content));
    }

    [Fact]
    public void Build_Events_FollowTheSystemBlocks() =>
        Assert.Equal("event_1", Build(Manager(), EventsOf(new UserEvent { Content = "x" })).Blocks[^1].Key);

    [Fact]
    public void Build_EmptyResponse_IsKeptInTheLogButNotSent()
    {
        var events = EventsOf(new UserEvent { Content = "x" }, new Nooa.Llm.LlmResponse());

        Assert.Single(Build(Manager(), events).Blocks, b => b.Event is not null);
    }

    [Fact]
    public void Build_ArchivedEvents_AreNotInTheContext()
    {
        var events = EventsOf(
            new UserEvent { Content = "a" }, new UserEvent { Content = "b" }, new UserEvent { Content = "c" }, new UserEvent { Content = "d" });
        events.Collapse("2", "3", "summary");

        var result = Build(Manager(), events);
        var tags = result.Blocks.Where(b => b.Event is not null).Select(b => b.Metadata.Tag).ToList();

        Assert.Equal(["1", "2..3", "4"], tags);
        Assert.IsType<Summary>(result.Blocks.Single(b => b.Metadata.Tag == "2..3").Event);
    }

    [Fact]
    public void Build_EventQuery_Priority_IsRuntimeThenScopeThenAgent()
    {
        var events = EventsOf(new TaskEvent { Prompt = "t" }, new Error { Content = "e" }, new Message { Content = "m" });
        var onlyTasks = EventQuery.ByType("Task");
        var onlyErrors = EventQuery.ByType("Error");
        var onlyMessages = EventQuery.ByType("Message");

        Assert.Equal("Task", Types(Build(Manager(), events, runtimeQuery: onlyTasks, scopedQuery: onlyErrors, agentQuery: onlyMessages)).Single());
        Assert.Equal("Error", Types(Build(Manager(), events, scopedQuery: onlyErrors, agentQuery: onlyMessages)).Single());
        Assert.Equal("Message", Types(Build(Manager(), events, agentQuery: onlyMessages)).Single());
        Assert.Equal(3, Types(Build(Manager(), events)).Count);
    }

    [Fact]
    public void Build_EventQuery_CurrentResolvesToTheCurrentCall()
    {
        var inCall = new TaskEvent { Prompt = "mine", Metadata = new System.Text.Json.Nodes.JsonObject { ["call_id"] = "c1" } };
        var events = EventsOf(new Error { Content = "other" }, inCall);

        var result = Build(Manager(), events, agentQuery: EventQuery.CurrentCall(), callId: "c1");

        Assert.Equal("Task", Types(result).Single());
    }

    private static List<string> Types(BuildResult result) => [.. result.Blocks.Where(b => b.Event is not null).Select(b => b.Event!.EventType)];

    [Fact]
    public void FormatValue_NullFormat_IsUnbounded() =>
        Assert.Equal(new string('x', 5000), ContextBuilder.FormatValue(new string('x', 5000), null));
}
