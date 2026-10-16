// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Agentdoc;

namespace Nooa.Tests.Generated;

/// <summary>
/// Compiles <see cref="SampleAgent"/> with the generator as an analyzer and checks the generated code at run time.
/// Replaces the upstream <c>test_metaclass.py</c> and <c>test_decorator_strategy.py</c> cases that read decorator
/// metadata (see <c>docs/porting-notes.md</c>).
/// </summary>
public class SampleAgentTests
{
    [Fact]
    public async Task Method_WithResult_BuildsCallAndReturnsResult()
    {
        var agent = new SampleAgent { Result = 7 };
        using var cts = new CancellationTokenSource();

        var result = await agent.CountWordsAsync("a b c", cts.Token);

        Assert.Equal(7, result);
        var call = Assert.Single(agent.Calls);
        Assert.Equal("CountWordsAsync", call.MethodName);
        Assert.Equal("(string text) -> int", call.Signature);
        Assert.Equal("Counts the words in text.\n\nCount only letters.", call.Docstring);
        Assert.Equal(typeof(int), call.ReturnType);
        Assert.Null(call.StrategyType);
        var argument = Assert.Single(call.Arguments);
        Assert.Equal(new CallArgumentView("text", typeof(string), "a b c"), new CallArgumentView(argument.Name, argument.Type, argument.Value));
        Assert.Equal(cts.Token, Assert.Single(agent.Tokens));
        Assert.StartsWith("call_", call.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Method_WithoutResult_ExpandsPlaceholderAndKeepsArgumentOrder()
    {
        var agent = new SampleAgent { Company = "Globex" };

        await agent.GreetAsync("Ann", 2);

        var call = Assert.Single(agent.Calls);
        Assert.Equal("Greet the user of Globex.", call.Docstring);
        Assert.Null(call.ReturnType);
        Assert.Equal(["name", "times"], call.Arguments.Select(a => a.Name));
        Assert.Equal(["Ann", 2], call.Arguments.Select(a => a.Value));
        Assert.Equal(CancellationToken.None, Assert.Single(agent.Tokens));
    }

    [Fact]
    public async Task Method_WithStrategyAttribute_RecordsStrategyAndNullableTypes()
    {
        var agent = new SampleAgent { Result = "x" };

        var result = await agent.LookupAsync(null);

        Assert.Equal("x", result);
        var call = Assert.Single(agent.Calls);
        Assert.Equal(typeof(string), call.StrategyType);
        Assert.Equal(typeof(string), call.ReturnType);
        Assert.Equal("(string? key) -> string?", call.Signature);
        Assert.Null(Assert.Single(call.Arguments).Value);
    }

    [Fact]
    public async Task Method_WithKeywordParameterName_KeepsTheName()
    {
        var agent = new SampleAgent();

        await agent.FireAsync("boom");

        Assert.Equal("event", Assert.Single(Assert.Single(agent.Calls).Arguments).Name);
    }

    [Fact]
    public void Registry_AfterModuleInitializer_HoldsLlmAndRendersSystemPrompt()
    {
        Assert.True(AgentMetadataRegistry.TryGet(typeof(SampleAgent), out var metadata));

        Assert.Equal("fast-model", metadata!.Llm);
        var agent = new SampleAgent { Company = "Globex" };
        agent.Settings.Language = "French";
        Assert.Equal("You are a support agent for Globex. Answer in French. Use {braces} literally.", metadata.SystemPrompt!(agent));
    }

    [Fact]
    public void Registry_ForUnknownType_ReturnsFalse()
    {
        Assert.False(AgentMetadataRegistry.TryGet(typeof(RecordingAgent), out _));
    }

    [Fact]
    public void DocProvider_ForType_ReturnsClassSummary()
    {
        var text = AgentDocProviders.Default.GetSummary(typeof(SampleAgent));

        Assert.Equal("You are a support agent for {Company}. Answer in {Settings.Language}. Use {{braces}} literally.", text);
    }

    [Fact]
    public void DocProvider_ForMembers_ReturnsSummaries()
    {
        var provider = AgentDocProviders.Default;

        Assert.Equal("The company name.", provider.GetSummary(typeof(SampleAgent).GetField(nameof(SampleAgent.Company))!));
        Assert.Equal("Reply settings.", provider.GetSummary(typeof(SampleAgent).GetProperty(nameof(SampleAgent.Settings))!));
        Assert.Equal("Plain helper.", provider.GetSummary(typeof(SampleAgent).GetMethod(nameof(SampleAgent.Helper))!));
    }

    [Fact]
    public void DocProvider_ForParameters_ReturnsParamAndReturnsText()
    {
        var method = typeof(SampleAgent).GetMethod(nameof(SampleAgent.CountWordsAsync))!;

        Assert.Equal("The text to count.", AgentDocProviders.Default.GetParameterSummary(method.GetParameters()[0]));
        Assert.Equal("The token.", AgentDocProviders.Default.GetParameterSummary(method.GetParameters()[1]));
        Assert.Equal("The number of words.", AgentDocProviders.Default.GetParameterSummary(method.ReturnParameter));
    }

    [Fact]
    public void DocProvider_ForForeignMember_ReturnsNull()
    {
        var provider = AgentDocProviders.Default;

        Assert.Null(provider.GetSummary(typeof(RecordingAgent).GetProperty(nameof(RecordingAgent.Result))!));
    }

    [Fact]
    public void Doc_OfAgentType_ShowsGeneratedMethodsInSourceOrder()
    {
        var doc = AgentDocs.Doc(typeof(SampleAgent));

        var order = new[] { "CountWordsAsync", "GreetAsync", "FireAsync", "Helper" }
            .Select(name => doc.IndexOf(name, StringComparison.Ordinal))
            .ToList();
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.OrderBy(i => i), order);
    }

    private sealed record CallArgumentView(string Name, Type Type, object? Value);
}
