// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_codeact_return_result.py (intent)

using System.Text.Json;
using System.Text.Json.Nodes;
using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

public class ReturnResultValidatorTests
{
    private static readonly Agent _agent = new CodeActAgent(new AgentOptions { Llm = new FakeLlmClient() });

    private static ReturnResultValidator For(Type? returnType, Dictionary<string, object?>? variables = null, CodeActConfig? config = null) =>
        new(_agent, new CurrentCall { MethodName = "M", ReturnType = returnType }, new StubEngine(variables ?? []), config ?? new CodeActConfig(), new FormatConfig());

    [Fact]
    public void Check_InstanceOfTheType_IsAccepted()
    {
        var value = new OrderResult(true, 1, []);

        Assert.Same(value, For(typeof(OrderResult)).Check(value).Value);
    }

    [Fact]
    public void Check_IntForADouble_IsConverted()
    {
        var check = For(typeof(double)).Check(5);

        Assert.Equal(5.0, check.Value);
        Assert.IsType<double>(check.Value);
    }

    [Fact]
    public void Check_DictionaryForARecord_IsConvertedThroughJson()
    {
        var check = For(typeof(OrderResult)).Check(new Dictionary<string, object> { ["can_fulfill"] = true, ["total_cost"] = 2, ["unavailable_items"] = new[] { "a" } });

        Assert.Equal(new[] { "a" }, ((OrderResult)check.Value!).UnavailableItems);
    }

    [Fact]
    public void Check_WrongType_FailsWithTheExpectedType()
    {
        var check = For(typeof(int)).Check(new OrderResult(true, 1, []));

        Assert.False(check.IsValid);
        Assert.Contains("Expected: int", check.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(string), true)]
    public void Check_Null_IsAcceptedOnlyForNullableTypes(Type type, bool valid)
    {
        Assert.Equal(valid, For(type).Check(null).IsValid);
    }

    [Fact]
    public void Check_StringThatNamesAVariable_ReturnsTheVariable()
    {
        var order = new OrderResult(true, 4, []);

        Assert.Same(order, For(typeof(OrderResult), new() { ["order"] = order }).Check("order").Value);
    }

    [Fact]
    public void Check_StringForAStringMethod_IsNotResolvedAsAVariable()
    {
        Assert.Equal("order", For(typeof(string), new() { ["order"] = "other" }).Check("order").Value);
    }

    [Fact]
    public void Check_JsonArrayString_IsParsed()
    {
        Assert.Equal(new List<int> { 1, 2 }, For(typeof(List<int>)).Check("[1, 2]").Value);
    }

    [Fact]
    public void Check_StringThatIsNotJson_Fails()
    {
        Assert.False(For(typeof(List<int>)).Check("[1, ").IsValid);
    }

    [Fact]
    public void Check_JsonNodeAndJsonElement_AreDeserialized()
    {
        Assert.Equal(3, For(typeof(int)).Check(JsonNode.Parse("3")).Value);
        Assert.Equal(4, For(typeof(int)).Check(JsonDocument.Parse("4").RootElement).Value);
    }

    [Fact]
    public void Check_VoidMethod_AcceptsOnlyNull()
    {
        Assert.True(For(null).Check(null).IsValid);
        Assert.Contains("Method returns nothing", For(null).Check(5).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckToolArguments_FieldsWithoutResult_AreWrapped()
    {
        var check = For(typeof(OrderResult)).CheckToolArguments(JsonNode.Parse("""{"can_fulfill": true, "total_cost": 1, "unavailable_items": []}""")!.AsObject());

        Assert.True(check.IsValid);
    }

    [Fact]
    public void CheckToolArguments_NoArgumentsAtAll_Fails()
    {
        var check = For(typeof(int)).CheckToolArguments([]);

        Assert.Contains("'result' is missing", check.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckToolArguments_StringWrappedInExtraQuotes_IsUnwrapped()
    {
        var check = For(typeof(string)).CheckToolArguments(new JsonObject { ["result"] = "\"line1\\nline2\"" });

        Assert.Equal("line1\nline2", check.Value);
    }

    [Fact]
    public void CheckToolArguments_NullResultForVoid_IsAccepted()
    {
        Assert.True(For(null).CheckToolArguments(new JsonObject { ["result"] = null }).IsValid);
        Assert.True(For(null).CheckToolArguments([]).IsValid);
    }

    [Fact]
    public void CheckToolArguments_TypeWithoutJsonForm_ExplainsTheCodePath()
    {
        var check = For(typeof(Func<int>)).CheckToolArguments(new JsonObject { ["result"] = new JsonObject() });

        Assert.Contains("ReturnResult(variable)", check.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_PostconditionInvariant_FailsWithTheInvariantText()
    {
        var config = new CodeActConfig { Postconditions = [(_, _, _) => throw new InvariantException("nope")] };

        var check = For(typeof(int), config: config).Check(1);

        Assert.StartsWith("Method invariant failed: nope", check.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_PostconditionSeesTheTypedValue()
    {
        object? seen = null;
        var config = new CodeActConfig { Postconditions = [(_, result, _) => seen = result] };

        For(typeof(double), config: config).Check(2);

        Assert.Equal(2.0, seen);
    }

    [Fact]
    public void CorrectedArguments_ValidValue_IsWrappedAsResult()
    {
        var corrected = ReturnResultValidator.CorrectedArguments(new OrderResult(true, 1, []), []);

        Assert.True(corrected["result"]!["can_fulfill"]!.GetValue<bool>());
    }

    [Fact]
    public void CorrectedArguments_NullOrUnserializable_KeepsTheOriginal()
    {
        var original = new JsonObject { ["x"] = 1 };

        Assert.Same(original, ReturnResultValidator.CorrectedArguments(null, original));
        Assert.Same(original, ReturnResultValidator.CorrectedArguments(new Func<int>(() => 1), original));
    }

    private sealed class StubEngine(Dictionary<string, object?> variables) : IExecutionEngine
    {
        public int ExecutionCount => 0;

        public OutAccessor Out => throw new NotSupportedException();

        public bool IsAbandoned => false;

        public bool TryGetVariable(string name, out object? value) => variables.TryGetValue(name, out value);

        public Task DeclareArgumentsAsync(IReadOnlyList<ReplArgument> arguments, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CellResult> ExecuteAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CellResult(1, ResultStatus.Complete));
    }
}
