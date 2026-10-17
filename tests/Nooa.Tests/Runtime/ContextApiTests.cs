// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_context_api.py

using Nooa.Errors;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Runtime;

public class ContextApiTests
{
    private static NoteTakingAgent Build() => new(new AgentOptions { Llm = new FakeLlmClient() });

    [Fact]
    public void Indexer_StringValue_StoresVolatileText()
    {
        var api = Build().Context;

        api["notes"] = "Remember JSON";

        Assert.Equal("Remember JSON", api["notes"]);
        Assert.True(api.Contains("notes"));
        Assert.Equal(["notes"], api.Keys);
        Assert.Equal(1, api.Count);
        Assert.Equal("ContextApi(1 blocks)", api.ToString());
    }

    [Fact]
    public void Indexer_Null_HidesTheBlockAndRemovesIt()
    {
        var agent = Build();
        agent.Context["notes"] = "x";

        agent.Context["notes"] = null;

        Assert.False(agent.Context.Contains("notes"));
        Assert.False(agent.Context.IsEnabled("notes"));
    }

    [Fact]
    public void Indexer_MissingKey_Throws() => Assert.Throws<KeyNotFoundException>(() => Build().Context["nope"]);

    [Fact]
    public void GetOrDefault_MissingKey_ReturnsTheDefault() => Assert.Equal("d", Build().Context.GetOrDefault("nope", "d"));

    [Fact]
    public void Indexer_ProtectedBlock_IsReadableButNotWritableAndNotListed()
    {
        var agent = Build();

        // Readable by name: the block exists, and no turn has resolved its provider yet.
        Assert.Throws<Nooa.Errors.DynamicNotResolvedException>(() => agent.Context["system_prompt"]);
        Assert.False(agent.Context.Contains("system_prompt"));
        Assert.DoesNotContain("self", agent.Context.Keys);
        Assert.Throws<ProtectedBlockException>(() => agent.Context["system_prompt"] = "evil");
        Assert.Throws<ProtectedBlockException>(() => agent.Context.Set("state", expr: "\"evil\""));
        Assert.Throws<ProtectedBlockException>(() => agent.Context.Remove("self"));
        Assert.Throws<ProtectedBlockException>(() => agent.Context.Pop("self"));
    }

    [Fact]
    public void Set_ValueWithPrefix_UsesTheStaticPartition()
    {
        var agent = Build();

        agent.Context.Set("a", value: "x", prefix: true);
        agent.Context.Set("b", value: "y");

        Assert.True(agent.ContextManager.IsStatic("a"));
        Assert.False(agent.ContextManager.IsStatic("b"));
    }

    [Fact]
    public void Set_ValueAndExpression_Throws() =>
        Assert.Throws<ArgumentException>(() => Build().Context.Set("a", value: "x", expr: "1"));

    [Fact]
    public void Set_Neither_HidesTheBlock()
    {
        var agent = Build();
        agent.Context["a"] = "x";

        agent.Context.Set("a");

        Assert.False(agent.Context.Contains("a"));
    }

    [Fact]
    public void Set_Expression_StoresAProviderThatIsNotResolvedYet()
    {
        var agent = Build();

        agent.Context.Set("notes", expr: "self.RenderNotes()", prefix: true);

        Assert.True(agent.ContextManager.IsStatic("notes"));
        Assert.Throws<DynamicNotResolvedException>(() => agent.Context["notes"]);
    }

    [Fact]
    public void SetDynamic_Expression_StoresAVolatileProvider()
    {
        var agent = Build();

        agent.Context.SetDynamic("notes", "self.RenderNotes()");

        Assert.False(agent.ContextManager.IsStatic("notes"));
        Assert.True(agent.ContextManager.RawItems().Single(i => i.Key == "notes").Value.IsDynamic);
    }

    [Fact]
    public void Set_ExpressionThatIsNotCode_ThrowsAtSetTime()
    {
        var agent = Build();

        Assert.Throws<ArgumentException>(() => agent.Context.Set("a", expr: "var x = 1;"));
        Assert.Throws<RestrictedCodeException>(() => agent.Context.Set("a", expr: "System.IO.File.ReadAllText(\"/etc/hosts\")"));
        Assert.False(agent.Context.Contains("a"));
    }

    [Fact]
    public void RemoveAndPop_DeleteTheBlock()
    {
        var agent = Build();
        agent.Context["a"] = "1";
        agent.Context["b"] = "2";

        agent.Context.Remove("a");

        Assert.Equal("2", agent.Context.Pop("b"));
        Assert.Equal("d", agent.Context.Pop("b", "d"));
        Assert.Throws<KeyNotFoundException>(() => agent.Context.Remove("a"));
        Assert.Equal(0, agent.Context.Count);
    }

    [Fact]
    public void DisableAndEnable_HideAndShowABlock_ProtectedIncluded()
    {
        var agent = Build();
        agent.Context["a"] = "1";

        agent.Context.Disable("a", "self");

        Assert.Equal(new HashSet<string> { "a", "self" }, agent.Context.Disabled());
        Assert.False(agent.Context.IsEnabled("self"));
        agent.Context.Enable("a", "self");
        Assert.Empty(agent.Context.Disabled());
        Assert.True(agent.Context.IsEnabled("a"));
    }

    [Fact]
    public void AllKeys_IncludesProtectedAndStrategyKeys()
    {
        var keys = Build().Context.AllKeys();

        foreach (var key in new[] { "system_prompt", "self", "state", "strategy_prompt", "execution_context" })
        {
            Assert.Contains(key, keys);
        }
    }

    [Fact]
    public void Key_EmptyOrTooLong_Throws()
    {
        var api = Build().Context;

        Assert.Throws<ArgumentException>(() => api[""] = "x");
        Assert.Throws<ArgumentException>(() => api[new string('k', ContextApi.MaxKeyLength + 1)] = "x");
        Assert.Throws<ArgumentException>(() => api.Contains(new string('k', ContextApi.MaxKeyLength + 1)));
        api[new string('k', ContextApi.MaxKeyLength)] = "x";
    }

    [Fact]
    public void Context_IsHiddenFromTheModelUnlessTheAgentOptsIn()
    {
        var plain = Nooa.Agentdoc.AgentDocs.Doc(typeof(PlainAgent));
        var optedIn = Nooa.Agentdoc.AgentDocs.Doc(typeof(NoteTakingAgent));

        Assert.DoesNotContain("ContextApi", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("EventsApi", plain, StringComparison.Ordinal);
        Assert.Contains("ContextApi Context", optedIn, StringComparison.Ordinal);
        Assert.DoesNotContain("EventsApi", optedIn, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NullAgent_Throws() => Assert.Throws<ArgumentNullException>(() => new ContextApi(null!));
}
