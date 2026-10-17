// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/tests/test_predict_output_serialization.py and test_predict_param_guard.py

using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Llm;
using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class PredictStrategyTests
{
    private static (PredictAgent Agent, FakeLlmClient Fake) Create(PredictConfig? config = null)
    {
        var fake = new FakeLlmClient();
        var agent = new PredictAgent(new AgentOptions
        {
            Llm = fake,
            DefaultStrategy = config is null ? new PredictStrategy() : new PredictStrategy(config),
        });
        return (agent, fake);
    }

    private static string AllText(FakeLlmCall call) =>
        string.Join("\n", call.Messages.OfType<JsonMessage>().Select(m => m.Value["content"]!.GetValue<string>()));

    [Fact]
    public async Task Score_ScalarResult_WrapsTheRequestAndUnwrapsTheAnswer()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 7}");

        var result = await agent.ScoreAsync("great");

        Assert.Equal(7, result);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(typeof(int), call.OutputType);
        Assert.Null(call.Tools);
    }

    [Fact]
    public async Task List_ListResult_UnwrapsTheList()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": [\"a\", \"b\"]}");

        Assert.Equal(["a", "b"], await agent.ListAsync("a b"));
    }

    [Fact]
    public async Task Maybe_NullableResult_RequestsTheUnderlyingType()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 3}");

        Assert.Equal(3, await agent.MaybeAsync("x"));
        Assert.Equal(typeof(int), Assert.Single(fake.Calls).OutputType);
    }

    [Fact]
    public async Task Profile_RecordResult_ReturnsTheRecord()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"name\": \"Ann\", \"age\": 31}");

        var person = await agent.ProfileAsync("Ann is 31");

        Assert.Equal(new PredictPerson { Name = "Ann", Age = 31 }, person);
    }

    [Fact]
    public async Task Score_ResponseWithParsedValue_UsesTheParsedValue()
    {
        var (agent, fake) = Create();
        fake.EnqueueStructured(9);

        Assert.Equal(9, await agent.ScoreAsync("x"));
    }

    [Fact]
    public async Task Score_FirstRequest_HasTaskPromptAndStrategyInstructions()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 1}");

        await agent.ScoreAsync("great stuff");

        var text = AllText(Assert.Single(fake.Calls));
        Assert.Contains("# Your task\nScores the text.", text, StringComparison.Ordinal);
        Assert.Contains("## Input parameters:", text, StringComparison.Ordinal);
        Assert.Contains("great stuff", text, StringComparison.Ordinal);
        Assert.Contains("Perform the task and return the result directly.", text, StringComparison.Ordinal);
        Assert.Contains("You are generating structured data matching the specified return type.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Score_InvalidJson_RetriesWithJsonFeedbackAndTheRawOutput()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("this is not json").EnqueueText("{\"result\": 1}");

        var result = await agent.ScoreAsync("x");

        Assert.Equal(1, result);
        Assert.Equal(2, fake.CallCount);
        var retry = AllText(fake.Calls[1]);
        Assert.Contains("Validation error (attempt 1/10):", retry, StringComparison.Ordinal);
        Assert.Contains("could not be parsed as JSON", retry, StringComparison.Ordinal);
        Assert.Contains("Raw LLM output:\nthis is not json", retry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Profile_WrongFieldType_RetriesWithTheFieldPath()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"name\": \"Ann\", \"age\": \"old\"}").EnqueueText("{\"name\": \"Ann\", \"age\": 31}");

        var person = await agent.ProfileAsync("Ann");

        Assert.Equal(31, person.Age);
        var retry = AllText(fake.Calls[1]);
        Assert.Contains("failed validation", retry, StringComparison.Ordinal);
        Assert.Contains("Field `$.age`", retry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Score_MissingWrapper_RetriesWithTheTypeFeedback()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"value\": 1}").EnqueueText("{\"result\": 1}");

        Assert.Equal(1, await agent.ScoreAsync("x"));
        Assert.Contains("Field `$`", AllText(fake.Calls[1]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Score_EmptyResponse_ShowsAPlaceholderForTheRawOutput()
    {
        var (agent, fake) = Create();
        fake.Enqueue(new LlmResponse()).EnqueueText("{\"result\": 2}");

        Assert.Equal(2, await agent.ScoreAsync("x"));
        Assert.Contains("(empty response - both content and reasoning were empty)", AllText(fake.Calls[1]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Score_EveryAttemptFails_ThrowsGenerationExceptionWithoutModelText()
    {
        var (agent, fake) = Create(new PredictConfig { MaxRetries = 2 });
        fake.EnqueueText("secret-model-text").EnqueueText("secret-model-text");

        var error = await Assert.ThrowsAsync<GenerationException>(() => agent.ScoreAsync("x"));

        Assert.Equal(2, fake.CallCount);
        Assert.StartsWith("Structured output validation failed after 2 attempts.\nLast error:\n", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-model-text", error.Message, StringComparison.Ordinal);
        Assert.IsType<StructuredOutputException>(error.InnerException);
    }

    [Fact]
    public async Task Score_NoRetriesAllowed_ThrowsWithoutCallingTheModel()
    {
        var (agent, fake) = Create(new PredictConfig { MaxRetries = 0 });

        var error = await Assert.ThrowsAsync<GenerationException>(() => agent.ScoreAsync("x"));

        Assert.Equal("Structured output generation failed after 0 attempts", error.Message);
        Assert.Equal(0, fake.CallCount);
    }

    [Fact]
    public async Task Score_LongRawOutput_IsTruncatedInTheFeedback()
    {
        var (agent, fake) = Create(new PredictConfig { MaxErrorChars = 10 });
        fake.EnqueueText(new string('x', 50)).EnqueueText("{\"result\": 1}");

        await agent.ScoreAsync("x");

        var retry = AllText(fake.Calls[1]);
        Assert.Contains("Raw LLM output:\nxxxxxxxxxx\n... (truncated, 50 chars total)", retry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoResult_MethodWithoutResultType_ThrowsGenerationException()
    {
        var (agent, fake) = Create();

        var error = await Assert.ThrowsAsync<GenerationException>(() => agent.NoResultAsync("x"));

        Assert.Contains("NoResultAsync", error.Message, StringComparison.Ordinal);
        Assert.Contains("no return type", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, fake.CallCount);
    }

    [Fact]
    public async Task Score_RunsAsOneFinalTurn_WithBeforeAndAfterTurnEvents()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 1}");
        var events = new List<EventBase>();
        using var before = agent.EventManager.On("BeforeTurn", events.Add);
        using var after = agent.EventManager.On("AfterTurn", events.Add);

        await agent.ScoreAsync("x");

        var start = Assert.IsType<BeforeTurn>(events[0]);
        var end = Assert.IsType<AfterTurn>(events[1]);
        Assert.Equal("PREDICT", start.Strategy);
        Assert.Equal(1, start.TurnNumber);
        Assert.Equal("ScoreAsync", start.MethodName);
        Assert.Equal(start.GenerationId, end.GenerationId);
        Assert.True(end.IsFinal);
        Assert.True(end.Success);
        Assert.Null(end.ExceptionType);
    }

    [Fact]
    public async Task Score_Failure_ReportsTheExceptionTypeInTheAfterTurnEvent()
    {
        var (agent, _) = Create(new PredictConfig { MaxRetries = 1 });
        var ends = new List<AfterTurn>();
        using var after = agent.EventManager.On("AfterTurn", e => ends.Add((AfterTurn)e));

        await Assert.ThrowsAsync<GenerationException>(() => agent.ScoreAsync("x"));

        var end = Assert.Single(ends);
        Assert.False(end.Success);
        Assert.Equal(nameof(GenerationException), end.ExceptionType);
    }

    [Fact]
    public async Task Score_EventMode_KeepsOnlyTheResponseEvent()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 1}");

        await agent.ScoreAsync("x");

        Assert.DoesNotContain(agent.EventManager.Values(), e => e is ToolCallEvent);
        Assert.Single(agent.EventManager.Values(), e => e is LlmResponse);
    }

    [Fact]
    public async Task Profile_ToolCallMode_AppendsASyntheticReturnResultAfterTheResponse()
    {
        var (agent, fake) = Create(new PredictConfig { OutputSerialization = PredictOutputSerialization.ToolCall });
        fake.EnqueueText("{\"name\": \"Ann\", \"age\": 31}");

        await agent.ProfileAsync("Ann");

        var events = agent.EventManager.Values();
        var response = Assert.Single(events.OfType<LlmResponse>());
        var call = Assert.Single(events.OfType<ToolCallEvent>());
        Assert.True(events.ToList().IndexOf(response) < events.ToList().IndexOf(call));
        Assert.Equal("{\"name\": \"Ann\", \"age\": 31}", response.Content);
        Assert.Equal("return_result", call.Name);
        Assert.StartsWith("predict_", call.ToolCallId, StringComparison.Ordinal);
        Assert.Equal("{\"result\":{\"name\":\"Ann\",\"age\":31}}", call.Arguments.ToJsonString());
        Assert.NotNull(call.Result);
        Assert.Equal(call.ToolCallId, call.Result.ToolCallId);
        Assert.Equal("Result accepted.", call.Result.Content);
        Assert.Equal(ResultStatus.Complete, call.Result.ResultStatus);
        Assert.True(call.Metadata["synthetic"]!.GetValue<bool>());
        Assert.Equal("predict_return_result", call.Metadata["synthetic_type"]!.GetValue<string>());
    }

    [Fact]
    public async Task List_ToolCallMode_SerializesNestedValues()
    {
        var (agent, fake) = Create(new PredictConfig { OutputSerialization = PredictOutputSerialization.ToolCall });
        fake.EnqueueText("{\"result\": [\"b\", \"a\"]}");

        await agent.ListAsync("x");

        var call = Assert.Single(agent.EventManager.Values().OfType<ToolCallEvent>());
        Assert.Equal("{\"result\":[\"b\",\"a\"]}", call.Arguments.ToJsonString());
    }

    [Fact]
    public void New_Default_UsesEventSerialization()
    {
        Assert.Equal(PredictOutputSerialization.Event, new PredictStrategy().Config.OutputSerialization);
        Assert.Equal("PREDICT", new PredictStrategy().Name);
    }

    [Fact]
    public void New_InvalidConfig_Throws()
    {
        var config = new PredictConfig { Preconditions = [null!] };

        Assert.Throws<ConfigValidationException>(() => new PredictStrategy(config));
    }

    [Fact]
    public void GetBlockOverrides_HoldsTheStrategyPromptAsADynamicBlock()
    {
        var overrides = new PredictStrategy().GetBlockOverrides();

        var block = Assert.IsType<DynamicContext>(Assert.Contains("strategy_prompt", overrides!));
        Assert.StartsWith("You are generating structured data", (string)block.Provider()!, StringComparison.Ordinal);
    }

    // Param guard (test_predict_param_guard.py)

    [Fact]
    public async Task Score_OversizedString_ThrowsBeforeTheModelCall()
    {
        var (agent, fake) = Create(new PredictConfig { MaxParamChars = 1000 });

        var error = await Assert.ThrowsAsync<ArgumentException>(() => agent.ScoreAsync(new string('x', 2000)));

        Assert.Contains("'text'", error.Message, StringComparison.Ordinal);
        Assert.Contains("MaxParamChars=1,000", error.Message, StringComparison.Ordinal);
        Assert.Contains("raise PredictConfig.MaxParamChars", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, fake.CallCount);
        Assert.Empty(agent.EventManager.Values());
    }

    [Fact]
    public async Task Count_OversizedList_ThrowsWithTheParameterName()
    {
        var (agent, _) = Create(new PredictConfig { MaxParamChars = 500 });

        var error = await Assert.ThrowsAsync<ArgumentException>(() => agent.CountAsync(Enumerable.Range(0, 10_000).ToList()));

        Assert.Contains("'items'", error.Message, StringComparison.Ordinal);
        Assert.Contains("(rendered)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Count_SmallList_Passes()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 10}");

        Assert.Equal(10, await agent.CountAsync(Enumerable.Range(0, 10).ToList()));
    }

    [Fact]
    public async Task Score_NullLimit_DisablesTheGuard()
    {
        var (agent, fake) = Create(new PredictConfig { MaxParamChars = null });
        fake.EnqueueText("{\"result\": 1}");

        Assert.Equal(1, await agent.ScoreAsync(new string('x', 300_000)));
    }

    [Fact]
    public async Task Score_LongDocumentUnderTheDefaultLimit_RendersInFull()
    {
        var (agent, fake) = Create();
        fake.EnqueueText("{\"result\": 1}");
        var document = string.Concat(Enumerable.Repeat("word ", 30_000));

        await agent.ScoreAsync(document);

        Assert.Contains(document, AllText(Assert.Single(fake.Calls)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Score_CustomLimit_IsRespected()
    {
        var (small, _) = Create(new PredictConfig { MaxParamChars = 50 });
        var (large, fake) = Create(new PredictConfig { MaxParamChars = 200 });
        fake.EnqueueText("{\"result\": 1}");
        var text = new string('c', 100);

        await Assert.ThrowsAsync<ArgumentException>(() => small.ScoreAsync(text));
        Assert.Equal(1, await large.ScoreAsync(text));
    }
}
