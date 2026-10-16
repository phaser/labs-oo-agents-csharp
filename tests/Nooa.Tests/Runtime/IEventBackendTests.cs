// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_event_backend_protocol.py, tests/test_event_backend_roundtrip.py

using System.Text.Json.Nodes;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Json;
using Nooa.Llm;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

/// <summary>Contract tests that every <see cref="IEventBackend"/> must pass. A derived class supplies the backend.</summary>
public abstract class IEventBackendTests : IDisposable
{
    public static TheoryData<string, EventBase, Type, Role> AllEvents => new()
    {
        { "1", new UserEvent { Content = "hello from user" }, typeof(UserEvent), Role.User },
        { "2", new AssistantEvent { Content = "hello from assistant" }, typeof(AssistantEvent), Role.Assistant },
        { "3", new ToolCallEvent { ToolCallId = "tc-001", Name = "execute", Arguments = new JsonObject { ["code"] = "x = 42" } }, typeof(ToolCallEvent), Role.Assistant },
        { "4", new TaskEvent { Prompt = "Do the thing" }, typeof(TaskEvent), Role.User },
        { "5", new Message { Content = "Here is the answer" }, typeof(Message), Role.Assistant },
        { "6", new Error { Content = "NameError" }, typeof(Error), Role.User },
        { "7", new Feedback { Content = "Code executed successfully" }, typeof(Feedback), Role.User },
        { "8", new LlmResponse { Parts = [new AssistantText { Text = "result = compute()" }] }, typeof(LlmResponse), Role.Assistant },
        { "9", new CellOutput { ToolCallId = "tc-001", ExecutionStatus = ResultStatus.Complete, ExecutionCount = 1, Stdout = "42\n" }, typeof(CellOutput), Role.User },
        { "10", new Summary { SummaryTag = "2..10", ReplacedRange = new SeqRange(2, 10), SummaryText = "Explored." }, typeof(Summary), Role.Assistant },
        { "11", new BeforeTurn { MethodName = "respond", Strategy = "CodeAct", GenerationId = "g1", TurnNumber = 1 }, typeof(BeforeTurn), Role.RuntimeEvent },
        { "12", new AfterTurn { MethodName = "respond", Strategy = "CodeAct", GenerationId = "g1", TurnNumber = 1, IsFinal = false }, typeof(AfterTurn), Role.RuntimeEvent },
    };

    protected abstract IEventBackend Backend { get; }

    public virtual void Dispose() => GC.SuppressFinalize(this);

    private static UserEvent User(string content) => new() { Content = content };

    [Fact]
    public void StoreAndGet_RoundTripsContent()
    {
        Backend.Store("1", User("hello"));

        Assert.Equal("hello", Assert.IsType<UserEvent>(Backend.Get("1")).Content);
    }

    [Fact]
    public void Get_MissingTag_ReturnsNull()
    {
        Backend.Store("1", User("x"));

        Assert.Null(Backend.Get("99"));
    }

    [Fact]
    public void GetById_FindsEvent()
    {
        var e = User("find me by id");
        Backend.Store("1", e);

        Assert.Equal("find me by id", Assert.IsType<UserEvent>(Backend.GetById(e.Id)).Content);
    }

    [Fact]
    public void GetById_Missing_ReturnsNull() => Assert.Null(Backend.GetById("no-such-id"));

    [Fact]
    public void Remove_Present_ReturnsTrueAndRemoves()
    {
        Backend.Store("1", User("to remove"));

        Assert.True(Backend.Remove("1"));
        Assert.Null(Backend.Get("1"));
    }

    [Fact]
    public void Remove_Missing_ReturnsFalse() => Assert.False(Backend.Remove("nonexistent"));

    [Fact]
    public void Remove_AlsoRemovesFromActiveTags()
    {
        Backend.Store("1", User("a"));
        Backend.Store("2", User("b"));

        Backend.Remove("1");

        Assert.Equal(["2"], Backend.ActiveTags());
    }

    [Fact]
    public void Update_ChangesMetadata_AndKeepsExisting()
    {
        var e = User("x");
        e.Metadata["existing"] = "value";
        Backend.Store("1", e);

        Assert.True(Backend.Update("1", stored => stored.Metadata["new_key"] = "new_value"));

        var result = Backend.Get("1")!;
        Assert.Equal("value", (string?)result.Metadata["existing"]);
        Assert.Equal("new_value", (string?)result.Metadata["new_key"]);
    }

    [Fact]
    public void Update_Missing_ReturnsFalseAndSkipsDelegate()
    {
        var called = false;

        Assert.False(Backend.Update("99", _ => called = true));
        Assert.False(called);
    }

    [Fact]
    public void Update_Status_ChangesStatusButNotActiveTags()
    {
        Backend.Store("1", User("a"));
        Backend.Store("2", User("b"));

        Backend.Update("1", stored => stored.Status = EventStatus.Archived);

        Assert.Equal(EventStatus.Archived, Backend.Get("1")!.Status);
        Assert.Equal(["1", "2"], Backend.ActiveTags());
    }

    [Fact]
    public void Update_ToolResult_SurvivesRoundTrip()
    {
        Backend.Store("upd", new ToolCallEvent { ToolCallId = "tc-upd", Name = "execute", Arguments = new JsonObject { ["code"] = "x = 1" } });

        Backend.Update("upd", stored => ((ToolCallEvent)stored).Result =
            new ToolResult { ToolCallId = "tc-upd", Content = "", ResultStatus = ResultStatus.Complete });

        var back = Assert.IsType<ToolCallEvent>(Backend.Get("upd"));
        Assert.Equal(ResultStatus.Complete, back.Result!.ResultStatus);
    }

    [Fact]
    public void Count_CountsAllEvents_IncludingArchived()
    {
        Assert.Equal(0, Backend.Count);
        Backend.Store("1", User("a"));
        Backend.Store("2", User("b"));
        Backend.SetStatus("1", EventStatus.Archived);

        Assert.Equal(2, Backend.Count);
    }

    [Fact]
    public void Clear_RemovesEverything_AndResetsTagCounter()
    {
        Backend.Store("1", User("a"));
        Backend.Store("2", User("b"));

        Backend.Clear();

        Assert.Equal(0, Backend.Count);
        Assert.Empty(Backend.ActiveTags());
        Assert.Empty(Backend.AllEvents());
        Assert.Equal("1", Backend.AllocateNextTag());
    }

    [Fact]
    public void ActiveTags_KeepInsertionOrder()
    {
        Backend.Store("1", User("a"));
        Backend.Store("2", new AssistantEvent { Content = "b" });
        Backend.Store("3", User("c"));

        Assert.Equal(["1", "2", "3"], Backend.ActiveTags());
    }

    [Fact]
    public void SetStatus_Archived_KeepsActiveTagsAndEvent()
    {
        Backend.Store("1", User("a"));
        Backend.Store("2", User("b"));

        Assert.True(Backend.SetStatus("1", EventStatus.Archived));

        Assert.Equal(["1", "2"], Backend.ActiveTags());
        Assert.Equal(EventStatus.Archived, Assert.IsType<UserEvent>(Backend.Get("1")).Status);
    }

    [Fact]
    public void SetStatus_Missing_ReturnsFalse() => Assert.False(Backend.SetStatus("99", EventStatus.Archived));

    [Fact]
    public void RemoveActiveTag_ReturnsWhetherPresent_AndKeepsEvent()
    {
        Backend.Store("1", User("stays"));

        Assert.False(Backend.RemoveActiveTag("nonexistent"));
        Assert.True(Backend.RemoveActiveTag("1"));
        Assert.DoesNotContain("1", Backend.ActiveTags());
        Assert.NotNull(Backend.Get("1"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InsertActiveTag_PutsTagAtPosition(int index)
    {
        Backend.Store("1", User("a"));
        Backend.Store("2", User("b"));
        Backend.Store("3", User("c"));
        string[] others = ["1", "2", "3"];
        var moved = others[index];
        Backend.RemoveActiveTag(moved);

        Backend.InsertActiveTag(moved, index);

        Assert.Equal(["1", "2", "3"], Backend.ActiveTags());
    }

    [Fact]
    public void FindTag_ReturnsTag_ForStoredEvent()
    {
        var e = User("findable");
        Backend.Store("42", e);

        Assert.Equal("42", Backend.FindTag(e));
    }

    [Fact]
    public void FindTag_UnstoredOrRemoved_ReturnsNull()
    {
        var never = User("never stored");
        var removed = User("stored then removed");
        Backend.Store("1", removed);
        Backend.Remove("1");

        Assert.Null(Backend.FindTag(never));
        Assert.Null(Backend.FindTag(removed));
    }

    [Fact]
    public void AllEvents_InInsertionOrder_IncludingArchived()
    {
        Backend.Store("1", User("first"));
        Backend.Store("2", new AssistantEvent { Content = "second" });
        Backend.Store("3", User("third"));
        Backend.SetStatus("2", EventStatus.Archived);

        var events = Backend.AllEvents().ToList();

        Assert.Equal(3, events.Count);
        Assert.Equal("first", ((UserEvent)events[0]).Content);
        Assert.Equal("second", ((AssistantEvent)events[1]).Content);
        Assert.Equal("third", ((UserEvent)events[2]).Content);
    }

    [Fact]
    public void AllEvents_Empty_ReturnsNothing() => Assert.Empty(Backend.AllEvents());

    [Fact]
    public void ToolCallEvent_WithResult_RoundTripsFieldForField()
    {
        Backend.Store("1", new ToolCallEvent
        {
            ToolCallId = "tc2",
            Name = "run_code",
            Arguments = new JsonObject { ["code"] = "print(1)" },
            Result = new ToolResult { ToolCallId = "tc2", Content = "1\n", ResultStatus = ResultStatus.Complete },
        });

        var back = Assert.IsType<ToolCallEvent>(Backend.Get("1"));

        Assert.Equal("run_code", back.Name);
        Assert.Equal("print(1)", (string?)back.Arguments["code"]);
        Assert.Equal("1\n", back.Result!.Content);
        Assert.Equal(ResultStatus.Complete, back.Result.ResultStatus);
    }

    [Theory]
    [MemberData(nameof(AllEvents))]
    public void Get_AfterStore_KeepsConcreteTypeAndRole(string tag, EventBase e, Type expectedType, Role expectedRole)
    {
        Backend.Store(tag, e);

        var back = Backend.Get(tag)!;

        Assert.Equal(expectedType, back.GetType());
        Assert.Equal(expectedRole, back.EventRole);
        Assert.Equal(e.EventType, back.EventType);
    }

    [Theory]
    [MemberData(nameof(AllEvents))]
    public void AllEvents_AfterStore_KeepsConcreteType(string tag, EventBase e, Type expectedType, Role expectedRole)
    {
        Assert.NotEqual(Role.Metadata, expectedRole);
        Backend.Store(tag, e);

        var back = Assert.Single(Backend.AllEvents());

        Assert.Equal(expectedType, back.GetType());
    }

    [Fact]
    public void LlmResponse_Parts_SurviveRoundTrip()
    {
        var response = new LlmResponse
        {
            Parts =
            [
                new AssistantReasoning { Text = "Check the inputs.", Native = new NativeJson(new JsonObject { ["opaque"] = "state" }) },
                new AssistantText { Text = "done" },
                new ToolCall { Id = "call-state", Name = "execute", Arguments = "{\"code\":\"print(1)\"}" },
            ],
            FinishReason = FinishReason.ToolCalls,
            Usage = new LlmUsage { InputTokens = 100, OutputTokens = 20, CachedInputTokens = 75 },
            ModelName = "provider/model",
            GenerationId = "generation-1",
        };
        Backend.Store("state", response);

        var back = Assert.IsType<LlmResponse>(Backend.Get("state"));

        Assert.Equal(response.Parts, back.Parts);
        Assert.Equal(response.FinishReason, back.FinishReason);
        Assert.Equal(response.Usage, back.Usage);
        Assert.Equal(("provider/model", "generation-1"), (back.ModelName, back.GenerationId));
    }

    [Fact]
    public void ArchivedStatus_SurvivesRoundTrip()
    {
        Backend.Store("1", User("old message"));
        Backend.SetStatus("1", EventStatus.Archived);

        Assert.Equal(EventStatus.Archived, Assert.IsType<UserEvent>(Backend.Get("1")).Status);
    }

    [Fact]
    public void AllocateNextTag_ReturnsSequentialTags()
    {
        Assert.Equal("1", Backend.AllocateNextTag());
        Assert.Equal("2", Backend.AllocateNextTag());
    }

    [Fact]
    public void Store_HigherTag_KeepsTagCounterCoherent()
    {
        Backend.AllocateNextTag();
        Backend.Store("99", User("pre-existing"));

        Assert.Equal("100", Backend.AllocateNextTag());
    }

    [Fact]
    public void Store_RangeTag_AdvancesCounterPastRangeEnd()
    {
        Backend.Store("2..40", User("range"));

        Assert.Equal("41", Backend.AllocateNextTag());
    }
}
