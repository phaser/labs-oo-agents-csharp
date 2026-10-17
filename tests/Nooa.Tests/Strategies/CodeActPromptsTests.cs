// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_codeact_prompts.py (intent)

using System.Text.Json.Nodes;
using Nooa.Runtime;
using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

public class CodeActPromptsTests
{
    private static CurrentCall Call(Type? returnType = null, params CallArgument[] arguments) =>
        new() { MethodName = "Run", Docstring = "Do the thing.", ReturnType = returnType, Arguments = arguments };

    [Fact]
    public void StrategyInstructions_DescribeBothToolsInCSharpTerms()
    {
        var text = CodeActPrompts.StrategyInstructions(RestrictedApiGroup.None);

        Assert.Contains("`execute_csharp(code)`", text, StringComparison.Ordinal);
        Assert.Contains("`return_result(value)`", text, StringComparison.Ordinal);
        Assert.Contains("ReturnResult(variable)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("python", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asyncio", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrategyInstructions_RestrictionsListOnlyTheGroupsThatAreNotAllowed()
    {
        var none = CodeActPrompts.StrategyInstructions(RestrictedApiGroup.None);
        var files = CodeActPrompts.StrategyInstructions(RestrictedApiGroup.FileSystem);

        Assert.Contains("Files and directories", none, StringComparison.Ordinal);
        Assert.DoesNotContain("Files and directories", files, StringComparison.Ordinal);
        Assert.Contains("Type declarations", files, StringComparison.Ordinal);
        Assert.Contains("Network and database clients", files, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionContext_ListsUsingsParametersAndGlobals()
    {
        var text = CodeActPrompts.ExecutionContext(
            typeof(CodeActAgent), Call(typeof(OrderResult), new CallArgument("items", typeof(List<string>), null)));

        Assert.Contains("using System.Linq;", text, StringComparison.Ordinal);
        Assert.Contains("using Nooa.Tests.Strategies.Acceptance;", text, StringComparison.Ordinal);
        Assert.Contains("`List<string> items`", text, StringComparison.Ordinal);
        Assert.Contains("`self` (the agent, a `CodeActAgent`)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionContext_NoArguments_HasNoParameterLine()
    {
        Assert.DoesNotContain("Task parameters", CodeActPrompts.ExecutionContext(typeof(CodeActAgent), Call()), StringComparison.Ordinal);
    }

    [Fact]
    public void TaskMessage_NamesTheMethodAndWarnsAgainstRecursion()
    {
        var text = CodeActPrompts.TaskMessage(Call());

        Assert.Contains("## Task: Run", text, StringComparison.Ordinal);
        Assert.Contains("Do the thing.", text, StringComparison.Ordinal);
        Assert.Contains("Calling `self.Run(...)` would recurse.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TextOnlyCorrection_WithValidationError_AppendsIt()
    {
        Assert.EndsWith("The attempted result was invalid:\nbad", CodeActPrompts.TextOnlyCorrection(Call(), "bad"), StringComparison.Ordinal);
        Assert.DoesNotContain("attempted result", CodeActPrompts.TextOnlyCorrection(Call()), StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyCodeError_NamesMisplacedKeys()
    {
        Assert.Equal("Execution error: empty code provided.", CodeActPrompts.EmptyCodeError(new JsonObject { ["code"] = "" }));
        Assert.Equal("Execution error: empty code provided.", CodeActPrompts.EmptyCodeError([]));
        Assert.Contains("received `cmd`", CodeActPrompts.EmptyCodeError(new JsonObject { ["cmd"] = "ls" }), StringComparison.Ordinal);
    }

    [Fact]
    public void CodeTool_RequiresACodeString()
    {
        var tool = CodeActPrompts.CodeTool();

        Assert.Equal("execute_csharp", tool.Name);
        Assert.Equal("string", (string)tool.ParametersSchema["properties"]!["code"]!["type"]!);
        Assert.Equal("code", (string)tool.ParametersSchema["required"]![0]!);
    }

    [Fact]
    public void ReturnTool_Scalar_WrapsTheTypeSchemaUnderResult()
    {
        var tool = CodeActPrompts.ReturnTool(typeof(int));

        Assert.Equal("integer", (string)tool.ParametersSchema["properties"]!["result"]!["type"]!);
        Assert.Contains("Expected return type: int", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnTool_Record_InlinesTheReferencedSchemas()
    {
        var tool = CodeActPrompts.ReturnTool(typeof(FeedbackAnalysis));

        Assert.NotNull(tool.ParametersSchema["properties"]!["result"]!["properties"]!["summary"]);
        Assert.Null(tool.ParametersSchema["$defs"]);
    }

    [Fact]
    public void ReturnTool_NullableValueType_UsesTheUnderlyingSchema()
    {
        var tool = CodeActPrompts.ReturnTool(typeof(int?));

        Assert.Contains("Expected return type: int?", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnTool_Void_HasNoRequiredResult()
    {
        var tool = CodeActPrompts.ReturnTool(null);

        Assert.Null(tool.ParametersSchema["required"]);
        Assert.Contains("No parameters required", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownToolFeedback_MissingMember_SuggestsDocSelf()
    {
        var text = CodeActPrompts.UnknownToolFeedback("nothing", [], typeof(CodeActAgent));

        Assert.Contains("Unknown tool `nothing`. Available tools: execute_csharp, return_result.", text, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"Print(Doc(self));\"", text, StringComparison.Ordinal);
        Assert.Contains("`self.nothing` does not exist", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownToolFeedback_Method_ShowsTheCallWithNamedLiteralArguments()
    {
        var args = new JsonObject { ["n"] = 4, ["flag"] = true, ["text"] = "a\"b" };

        var text = CodeActPrompts.UnknownToolFeedback("self.Twice", args, typeof(CodeActAgent));

        Assert.Contains("var result = self.Twice(n: 4, flag: true, text: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownToolFeedback_AsyncMethod_AddsAwait()
    {
        var text = CodeActPrompts.UnknownToolFeedback("AnswerAsync", new JsonObject { ["question"] = "q" }, typeof(CodeActAgent));

        Assert.Contains("var result = await self.AnswerAsync(question: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownToolFeedback_Object_ShowsDocOfTheObject()
    {
        var text = CodeActPrompts.UnknownToolFeedback("Todo", [], typeof(CodeActAgent));

        Assert.Contains("Print(Doc(self.Todo));", text, StringComparison.Ordinal);
        Assert.Contains("is an object, not a function", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownToolFeedback_PrivateName_IsTreatedAsMissingAndLongNamesAreBounded()
    {
        Assert.Contains("does not exist", CodeActPrompts.UnknownToolFeedback("_hidden", [], typeof(CodeActAgent)), StringComparison.Ordinal);
        Assert.Contains("...", CodeActPrompts.UnknownToolFeedback(new string('x', 200), [], typeof(CodeActAgent))[..140], StringComparison.Ordinal);
    }

    [Fact]
    public void Bound_ShortAndLong()
    {
        Assert.Equal("abc", CodeActPrompts.Bound("abc"));
        Assert.Equal(80, CodeActPrompts.Bound(new string('x', 500)).Length);
    }
}
