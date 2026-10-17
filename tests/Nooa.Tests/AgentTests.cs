// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/edge_cases/test_agent_initialization.py, tests/test_agent_llm_accessor.py, tests/test_method_call_permutations.py, tests/core_runtime/test_llm_client_reuse.py

#pragma warning disable xUnit1051 // The agentic methods take an optional token that these tests do not need.

using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Storage;
using Nooa.Tests.Runtime;

namespace Nooa.Tests;

[Collection(AgentStaticCollection.Name)]
public class AgentTests
{
    private static AgentOptions Options(FakeLlmClient llm, params Nooa.Strategies.GenerationStrategy[] strategies) =>
        new() { Llm = llm, Strategies = strategies };

    [Fact]
    public void Constructor_WithClient_CreatesFrameworkParts()
    {
        var llm = new FakeLlmClient();
        var agent = new EchoAgent(Options(llm));

        Assert.Same(llm, agent.Llm);
        Assert.NotEmpty(agent.AgentId);
        Assert.NotNull(agent.EventManager);
        Assert.NotNull(agent.Runtime);
        Assert.IsType<InMemoryStorageManager>(agent.Storage);
        Assert.Equal(["system_prompt", "self", "state"], agent.ContextManager.Keys);
        Assert.All(agent.ContextManager.Keys, k => Assert.True(agent.ContextManager.IsProtected(k)));
    }

    [Fact]
    public void Constructor_TwoAgents_HaveDifferentIds() =>
        Assert.NotEqual(new EchoAgent(Options(new FakeLlmClient())).AgentId, new EchoAgent(Options(new FakeLlmClient())).AgentId);

    [Fact]
    public void Constructor_SharesStorageBackend()
    {
        var storage = new InMemoryStorageManager();
        var agent = new EchoAgent(new AgentOptions { Llm = new FakeLlmClient(), Storage = storage });

        agent.EventManager.Add(new Message { Content = "x" });

        Assert.Equal(1, storage.EventBackend.Count);
    }

    [Fact]
    public void Constructor_NoLlmAnywhere_ThrowsWithResolutionSteps()
    {
        var e = Assert.Throws<NooaRuntimeException>(() => new EchoAgent());

        Assert.Contains("No LLM available for EchoAgent", e.Message);
        Assert.Contains("Instance", e.Message);
        Assert.Contains("Parent", e.Message);
    }

    [Fact]
    public void Constructor_ClientAndAlias_Throws() =>
        Assert.Throws<ArgumentException>(() => new EchoAgent(new AgentOptions { Llm = new FakeLlmClient(), LlmAlias = "x" }));

    [Fact]
    public void Constructor_Alias_UsesResolverOfOptions()
    {
        var resolver = new StubLlmResolver { Clients = { ["fast"] = new FakeLlmClient() } };

        var agent = new EchoAgent(new AgentOptions { LlmAlias = "fast", LlmResolver = resolver });

        Assert.Same(resolver.Clients["fast"], agent.Llm);
    }

    [Fact]
    public void Constructor_AliasWithoutResolver_ThrowsClearError()
    {
        var e = Assert.Throws<NooaRuntimeException>(() => new EchoAgent(new AgentOptions { LlmAlias = "fast" }));

        Assert.Contains("'fast'", e.Message);
        Assert.Contains("ILlmClientResolver", e.Message);
    }

    [Fact]
    public void Constructor_UnknownAlias_WrapsResolverError()
    {
        var e = Assert.Throws<NooaRuntimeException>(
            () => new EchoAgent(new AgentOptions { LlmAlias = "nope", LlmResolver = new StubLlmResolver() }));

        Assert.Contains("'nope'", e.Message);
        Assert.IsType<KeyNotFoundException>(e.InnerException);
    }

    [Fact]
    public void Constructor_ClassAlias_UsesDefaultResolver()
    {
        var resolver = new StubLlmResolver { Clients = { ["class-model"] = new FakeLlmClient() } };
        var previous = Agent.DefaultLlmResolver;
        Agent.DefaultLlmResolver = resolver;
        try
        {
            var agent = new ClassAliasAgent();

            Assert.Same(resolver.Clients["class-model"], agent.Llm);
        }
        finally
        {
            Agent.DefaultLlmResolver = previous;
        }
    }

    [Fact]
    public void Constructor_InstanceBeatsClass()
    {
        var llm = new FakeLlmClient();

        var agent = new ClassAliasAgent(new AgentOptions { Llm = llm });

        Assert.Same(llm, agent.Llm);
    }

    [Fact]
    public void Constructor_ClassAliasOnBaseClass_IsInherited()
    {
        var resolver = new StubLlmResolver { Clients = { ["class-model"] = new FakeLlmClient() } };

        var agent = new ClassAliasChildAgent(new AgentOptions { LlmResolver = resolver });

        Assert.Same(resolver.Clients["class-model"], agent.Llm);
    }

    [Fact]
    public async Task Constructor_InsideParentCall_InheritsParentLlm()
    {
        var parentLlm = new FakeLlmClient();
        var building = new ChildBuildingStrategy();
        var parent = new EchoAgent(Options(parentLlm, building));

        await parent.BuildChildAsync();

        Assert.Same(parentLlm, building.Built!.Llm);
    }

    [Fact]
    public void Truncation_MergesOptionsOverDefault()
    {
        var agent = new EchoAgent(new AgentOptions { Llm = new FakeLlmClient(), Truncation = new TruncationConfig { MaxContextTokens = 1234 } });

        Assert.Equal(1234, agent.Truncation.MaxContextTokens);
        Assert.Equal(TruncationConfig.Default.MinPreservedEvents, agent.Truncation.MinPreservedEvents);
    }

    [Fact]
    public void SetLlm_ReplacesClient()
    {
        var agent = new EchoAgent(Options(new FakeLlmClient()));
        var other = new FakeLlmClient();

        agent.SetLlm(other);

        Assert.Same(other, agent.Llm);
    }

    [Fact]
    public void ResolveSystemPrompt_UsesClassDocComment() =>
        Assert.Equal("You are a test agent.", new EchoAgent(Options(new FakeLlmClient())).ResolveSystemPrompt());

    [Fact]
    public void ResolveSystemPrompt_WithoutDocComment_UsesDefault() =>
        Assert.StartsWith("You are PlainAgent", new PlainAgent(Options(new FakeLlmClient())).ResolveSystemPrompt(), StringComparison.Ordinal);

    [Fact]
    public async Task Ask_CallsClientOnceAndReturnsText()
    {
        var llm = FakeLlmClient.SimpleMessage("hi there");
        var agent = new EchoAgent(Options(llm));

        var result = await agent.AskAsync("hello", TestContext.Current.CancellationToken);

        Assert.Equal("hi there", result);
        Assert.Equal(1, llm.CallCount);
    }

    [Fact]
    public async Task Ask_RecordsResponseEventWithCallId()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

        await agent.AskAsync("q", TestContext.Current.CancellationToken);

        var response = Assert.IsType<LlmResponse>(Assert.Single(agent.EventManager.Filter(type: "LLMResponse")));
        Assert.NotNull(response.Tag);
        Assert.False(string.IsNullOrEmpty(response.GenerationId));
        Assert.StartsWith("call_", response.Metadata["call_id"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_VoidAndNumberResults()
    {
        var agent = new EchoAgent(Options(new FakeLlmClient().EnqueueText("a").EnqueueText("42")));

        await agent.VoidAsync("q");

        await Assert.ThrowsAsync<GenerationException>(() => agent.NumberAsync("q"));
    }

    [Fact]
    public async Task Ask_NoDefaultStrategy_UsesCodeAct()
    {
        var fake = new FakeLlmClient().EnqueueToolCall("return_result", new { result = "done" });
        var agent = new EchoAgent(Options(fake));

        var result = await agent.DefaultAsync("q");

        Assert.Equal("done", result);
        Assert.Equal(["execute_csharp", "return_result"], Assert.Single(fake.Calls).Tools!.Select(t => t.Name));
    }

    [Fact]
    public async Task Ask_DefaultStrategy_IsUsedWithoutAttribute()
    {
        var agent = new EchoAgent(new AgentOptions { Llm = FakeLlmClient.SimpleMessage("d"), DefaultStrategy = new EchoStrategy() });

        Assert.Equal("d", await agent.DefaultAsync("q"));
    }

    [Fact]
    public async Task Ask_StrategyInstanceInOptions_BeatsNewInstance()
    {
        var strategy = new EchoStrategy();
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("x"), strategy));

        await agent.AskAsync("q");

        Assert.Single(strategy.GenerationIds);
    }

    [Fact]
    public async Task UseStrategy_OverridesAttributeInsideScopeOnly()
    {
        var agent = new EchoAgent(Options(new FakeLlmClient().EnqueueText("a").EnqueueText("b")));
        var override_ = new EchoStrategy();

        using (agent.UseStrategy(override_))
        {
            await agent.AskAsync("q");
        }

        await agent.AskAsync("q");

        Assert.Single(override_.GenerationIds);
    }

    [Fact]
    public async Task UseLlm_Client_OverridesInstanceClientInsideScopeOnly()
    {
        var instance = FakeLlmClient.SimpleMessage("instance");
        var call = FakeLlmClient.SimpleMessage("call");
        var agent = new EchoAgent(Options(instance));

        string scoped;
        using (agent.UseLlm(call))
        {
            scoped = await agent.AskAsync("q");
        }

        var after = await agent.AskAsync("q");

        Assert.Equal("call", scoped);
        Assert.Equal("instance", after);
    }

    [Fact]
    public async Task UseLlm_Alias_ResolvesAndWins()
    {
        var resolver = new StubLlmResolver { Clients = { ["big"] = FakeLlmClient.SimpleMessage("big") } };
        var agent = new EchoAgent(new AgentOptions { Llm = FakeLlmClient.SimpleMessage("small"), LlmResolver = resolver });

        using var scope = agent.UseLlm("big");

        Assert.Equal("big", await agent.AskAsync("q"));
    }

    [Fact]
    public async Task MethodLevelAlias_BeatsInstanceClient_AndLosesToCallScope()
    {
        var resolver = new StubLlmResolver { Clients = { ["method-model"] = FakeLlmClient.SimpleMessage("method") } };
        var agent = new EchoAgent(new AgentOptions { Llm = FakeLlmClient.SimpleMessage("instance"), LlmResolver = resolver });

        Assert.Equal("method", await agent.MethodModelAsync("q"));

        using (agent.UseLlm(FakeLlmClient.SimpleMessage("call")))
        {
            Assert.Equal("call", await agent.MethodModelAsync("q"));
        }
    }

    [Fact]
    public async Task MethodLevelAlias_IsCachedPerInstance()
    {
        var resolver = new StubLlmResolver { Clients = { ["method-model"] = new FakeLlmClient().EnqueueText("a").EnqueueText("b") } };
        var agent = new EchoAgent(new AgentOptions { Llm = new FakeLlmClient(), LlmResolver = resolver });

        await agent.MethodModelAsync("q");
        await agent.MethodModelAsync("q");

        Assert.Single(resolver.Requests);
    }

    [Fact]
    public async Task Nested_CallInsideStrategy_RunsInlineWithoutDeadlock()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("inner-answer")));

        var result = await agent.OuterAsync("q").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("outer:inner-answer", result);
    }

    [Fact]
    public async Task Nested_EventsLinkParentCallId()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("x")));
        var started = new List<BeforeAgentCall>();
        using var subscription = agent.EventManager.On("BeforeAgentCall", e => started.Add((BeforeAgentCall)e));

        await agent.OuterAsync("q");

        Assert.Equal(["OuterAsync", "AskAsync"], started.Select(s => s.MethodName));
        Assert.Null(started[0].ParentCallId);
        Assert.True(started[0].IsTopLevel);
        Assert.Equal(started[0].CallId, started[1].ParentCallId);
        Assert.False(started[1].IsTopLevel);
    }

    [Fact]
    public async Task NestingDepth_ExceedsLimit_Throws()
    {
        var agent = new EchoAgent(new AgentOptions
        {
            Llm = FakeLlmClient.SimpleMessage("x"),
            Execution = new ExecutionConfig { MaxNestingDepth = 1 },
        });

        var e = await Assert.ThrowsAsync<NooaRuntimeException>(() => agent.OuterAsync("q"));

        Assert.Contains("maximum nesting depth of 1", e.Message);
    }

    [Fact]
    public async Task Lock_StrategyRequiringLock_SerializesParallelCalls()
    {
        var gate = new GateStrategy(requiresLock: true);
        var agent = new EchoAgent(Options(new FakeLlmClient(), gate));

        await Task.WhenAll(agent.GatedAsync("a"), agent.GatedAsync("b"), agent.GatedAsync("c"));

        Assert.Equal(1, gate.MaxConcurrent);
    }

    [Fact]
    public async Task Lock_StrategyWithoutLock_RunsInParallel()
    {
        var gate = new GateStrategy(requiresLock: false);
        var agent = new EchoAgent(Options(new FakeLlmClient(), gate));

        await Task.WhenAll(agent.GatedAsync("a"), agent.GatedAsync("b"), agent.GatedAsync("c"));

        Assert.True(gate.MaxConcurrent > 1);
    }

    [Fact]
    public async Task Lock_CancelledWhileWaiting_ThrowsAndReleases()
    {
        var gate = new GateStrategy(requiresLock: true);
        var agent = new EchoAgent(Options(new FakeLlmClient(), gate));
        var first = agent.GatedAsync("a");
        using var cts = new CancellationTokenSource(10);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.GatedAsync("b", cts.Token));
        await first;

        Assert.Equal("done", await agent.GatedAsync("c"));
    }

    [Fact]
    public async Task Failure_EmitsAfterAgentCallWithException_AndReleasesLock()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));
        var ended = new List<AfterAgentCall>();
        using var subscription = agent.EventManager.On("AfterAgentCall", e => ended.Add((AfterAgentCall)e));

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.FailAsync("q"));
        await agent.AskAsync("q");

        Assert.False(ended[0].Success);
        Assert.Equal("InvalidOperationException", ended[0].ExceptionType);
        Assert.True(ended[1].Success);
    }

    [Fact]
    public async Task AgentCallEvents_AreNotRecorded()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

        await agent.AskAsync("q");

        Assert.Empty(agent.EventManager.Filter(type: "BeforeAgentCall"));
        Assert.Empty(agent.EventManager.Filter(type: "LLMCallStart"));
    }

    [Fact]
    public async Task LlmCallEvents_AreEmittedInOrder()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));
        var seen = new List<string>();
        using var subscription = agent.EventManager.On("*", e => seen.Add(e.EventType));

        await agent.AskAsync("q");

        Assert.Equal(["BeforeAgentCall", "LLMCallStart", "LLMCallEnd", "LLMResponse", "AfterAgentCall"], seen);
    }

    [Fact]
    public async Task Close_RunsCleanup()
    {
        var agent = new EchoAgent(Options(new FakeLlmClient()));
        var ran = false;
        agent.EventManager.OnClose(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        await agent.CloseAsync();

        Assert.True(ran);
    }

    [Fact]
    public async Task Middleware_AgentCall_WrapsMethod()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("model")));
        agent.EventManager.Intercept<AgentCallContext>(async (ctx, next) =>
        {
            ctx = await next(ctx);
            ctx.Result = ((string)ctx.Result!) + "+mw";
            return ctx;
        });

        Assert.Equal("model+mw", await agent.AskAsync("q"));
    }

    [Fact]
    public async Task Middleware_AgentCall_ShortCircuitSkipsModel()
    {
        var llm = new FakeLlmClient();
        var agent = new EchoAgent(Options(llm));
        agent.EventManager.Intercept<AgentCallContext>((ctx, _) =>
        {
            ctx.Result = "blocked";
            return Task.FromResult(ctx);
        });

        Assert.Equal("blocked", await agent.AskAsync("q"));
        Assert.Equal(0, llm.CallCount);
    }

    [Fact]
    public async Task Middleware_AgentCall_WithoutResult_Throws()
    {
        var agent = new EchoAgent(Options(new FakeLlmClient()));
        agent.EventManager.Intercept<AgentCallContext>((ctx, _) => Task.FromResult(ctx));

        var e = await Assert.ThrowsAsync<NooaRuntimeException>(() => agent.AskAsync("q"));

        Assert.Contains("without setting the result", e.Message);
    }

    [Fact]
    public async Task Middleware_LlmCall_CanEditMessagesAndSeeClient()
    {
        var llm = FakeLlmClient.SimpleMessage("ok");
        var agent = new EchoAgent(Options(llm));
        Nooa.Llm.ILlmClient? seen = null;
        agent.EventManager.Intercept<LlmCallContext>(async (ctx, next) =>
        {
            seen = ctx.Client;
            ctx.Messages = [LlmMessage.User("edited")];
            return await next(ctx);
        });

        await agent.AskAsync("original");

        Assert.Same(llm, seen);
        Assert.Contains("edited", llm.LastMessages.Single().ToString());
    }

    [Fact]
    public async Task Middleware_LlmCall_ShortCircuitWithResponse()
    {
        var llm = new FakeLlmClient();
        var agent = new EchoAgent(Options(llm));
        agent.EventManager.Intercept<LlmCallContext>((ctx, _) =>
        {
            ctx.Response = new LlmResponse { Parts = [new AssistantText { Text = "canned" }] };
            return Task.FromResult(ctx);
        });

        Assert.Equal("canned", await agent.AskAsync("q"));
        Assert.Equal(0, llm.CallCount);
    }

    [Fact]
    public async Task Hooks_BeforeAndAfter_FireForTracedCallsOnly()
    {
        var hooks = new RecordingHooks();
        InstrumentationHooks.Current = hooks;
        try
        {
            var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

            await agent.AskAsync("q");
            await agent.QuietAsync("q");

            Assert.Equal(["before:AskAsync", "beforeGen:EchoStrategy", "afterGen", "after:AskAsync:ok", "beforeGen:EchoStrategy", "afterGen"], hooks.Log);
        }
        finally
        {
            InstrumentationHooks.Current = null;
        }
    }

    [Fact]
    public async Task Hooks_ThatThrow_DoNotAffectTheCall()
    {
        InstrumentationHooks.Current = new RecordingHooks { Throw = true };
        try
        {
            var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

            Assert.Equal("ok", await agent.AskAsync("q"));
        }
        finally
        {
            InstrumentationHooks.Current = null;
        }
    }

    [Fact]
    public async Task EventFormat_IsCopiedIntoEventMetadata()
    {
        var agent = new EchoAgent(Options(FakeLlmClient.SimpleMessage("ok")));

        await agent.AskAsync("q");

        var response = agent.EventManager.Filter(type: "LLMResponse").Single();
        Assert.NotNull(response.Metadata["truncation_event_format"]);
    }

    [Fact]
    public async Task Strategy_TypeThatIsNotAStrategy_FailsClearly()
    {
        var agent = new BadStrategyAgent(Options(new FakeLlmClient()));

        var e = await Assert.ThrowsAsync<NooaRuntimeException>(() => agent.BadAsync("q"));

        Assert.Contains("must derive from GenerationStrategy", e.Message);
    }

    [Fact]
    public void Constructor_NoRenderOptions_UsesTheCachedFormatterAndNoQuery()
    {
        var agent = new EchoAgent(Options(new FakeLlmClient()));

        Assert.IsType<CachedBlockFormatter>(agent.BlockFormatter);
        Assert.Null(agent.EventQuery);
    }

    [Fact]
    public void Constructor_RenderOptions_AreKept()
    {
        var formatter = new XmlBlockFormatter();
        var query = EventQuery.LastN(5);

        var agent = new EchoAgent(new AgentOptions { Llm = new FakeLlmClient(), BlockFormatter = formatter, EventQuery = query });

        Assert.Same(formatter, agent.BlockFormatter);
        Assert.Same(query, agent.EventQuery);
    }

    [Fact]
    public void Constructor_ContextOverrides_ApplyToProtectedAndNewBlocks()
    {
        var agent = new EchoAgent(new AgentOptions
        {
            Llm = new FakeLlmClient(),
            Context = new Dictionary<string, object?>
            {
                ["system_prompt"] = "custom prompt",
                ["state"] = null,
                ["rules"] = Context.Of("be brief", prefix: true),
                ["status"] = new DynamicContext(() => "ok"),
            },
        });

        Assert.Equal("custom prompt", agent.ContextManager.Get("system_prompt"));
        Assert.True(agent.ContextManager.IsDisabled("state"));
        Assert.True(agent.ContextManager.IsStatic("rules"));
        Assert.True(agent.ContextManager.Contains("status"));
        Assert.True(agent.ContextManager.IsProtected("system_prompt"));
    }
}
