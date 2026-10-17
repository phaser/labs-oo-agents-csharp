// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_execute_code.py, tests/runtime/test_cancelled_cell_output.py (intent)

using Nooa.ContextBlocks;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class RoslynExecutionEngineTests
{
    private static RoslynExecutionEngine NewEngine(ReplOptions? options = null, ReplAgent? agent = null) =>
        new(new ReplGlobals<ReplAgent>(agent ?? new ReplAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() })), options);

    private static ReplOptions WithTestAssembly(ReplOptions? o = null) =>
        (o ?? new ReplOptions()) with { References = [typeof(ReplSpin).Assembly] };

    // The cell time limit also covers the compile of the cell. A loaded CI runner can need more than a second
    // for a first compile, so the timing tests use a limit that is wide against that.
    private static readonly TimeSpan _wideTimeout = TimeSpan.FromSeconds(3);

    private static Task<CellResult> Run(RoslynExecutionEngine engine, string code) =>
        engine.ExecuteAsync(code, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExecuteAsync_LastExpression_ReturnsImplicitValue()
    {
        var result = await Run(NewEngine(), "1 + 2");

        Assert.Equal(ResultStatus.Complete, result.Status);
        Assert.Equal(3, result.Value);
        Assert.False(result.ExplicitReturn);
        Assert.Equal(1, result.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_VariableFromEarlierCell_Persists()
    {
        var engine = NewEngine();
        await Run(engine, "var x = 40;");

        var result = await Run(engine, "x + 2");

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_AsyncCode_AwaitsAtTopLevel()
    {
        var result = await Run(NewEngine(), "await Task.Delay(1); 7");

        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_CompileError_KeepsStateAndReportsCellLocation()
    {
        var engine = NewEngine();
        await Run(engine, "var x = 5;");

        var bad = await Run(engine, "var ok = 1;\nvar q = ;");
        var after = await Run(engine, "x");

        Assert.Equal(ResultStatus.Error, bad.Status);
        Assert.Contains("Cell In[2], line 2", bad.Error);
        Assert.Contains("var q = ;", bad.Error);
        Assert.Contains("error CS1525", bad.Error);
        Assert.Equal(5, after.Value);
        Assert.Equal(3, after.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_RuntimeException_KeepsVariablesAssignedBeforeTheThrow()
    {
        var engine = NewEngine();

        var failed = await Run(engine, "var a = 1;\nvar b = 2;\nthrow new InvalidOperationException(\"boom\");");
        var after = await Run(engine, "a + b");

        Assert.Equal(ResultStatus.Error, failed.Status);
        Assert.Contains("Cell In[1], line 3, in <cell>", failed.Error);
        Assert.Contains("throw new InvalidOperationException(\"boom\");", failed.Error);
        Assert.EndsWith("InvalidOperationException: boom", failed.Error);
        Assert.DoesNotContain("Microsoft.CodeAnalysis", failed.Error);
        Assert.DoesNotContain("TaskAwaiter", failed.Error);
        Assert.Equal(3, after.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionInUserHelper_KeepsTheHelperFrame()
    {
        var result = await Run(NewEngine(WithTestAssembly()), "Nooa.Tests.Runtime.ReplHelper.Boom();");

        Assert.Contains("Cell In[1], line 1", result.Error);
        Assert.Contains("at Nooa.Tests.Runtime.ReplHelper.Boom", result.Error);
        Assert.EndsWith("InvalidOperationException: helper failed", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorInLocalFunction_NamesTheFunction()
    {
        var result = await Run(NewEngine(), "void Fail()\n{\n    throw new Exception(\"x\");\n}\nFail();");

        Assert.Contains("line 3, in Fail", result.Error);
        Assert.Contains("line 5, in <cell>", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_PrintAndConsole_AreCapturedPerCell()
    {
        var engine = NewEngine();
        var before = System.Console.Out;

        var first = await Run(engine, "Print(\"a\"); Console.WriteLine(\"b {0}\", 1); Console.Error.WriteLine(\"e\"); Console.Write(\"c\");");
        var second = await Run(engine, "Print(\"z\");");

        Assert.Equal("a\nb 1\nc".Replace("\n", Environment.NewLine), first.Stdout);
        Assert.Equal("e" + Environment.NewLine, first.Stderr);
        Assert.Equal("z" + Environment.NewLine, second.Stdout);
        Assert.Same(before, System.Console.Out);
    }

    [Fact]
    public async Task ExecuteAsync_LargeOutput_IsCutToTheCaptureBudget()
    {
        var options = new ReplOptions { Capture = new Nooa.Config.CaptureConfig { MaxStdout = 100 } };

        var result = await Run(NewEngine(options), "Print(new string('x', 5000));");

        Assert.True(result.Stdout.Length < 600);
        Assert.Contains("x", result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnResult_EndsTheCellWithAnExplicitValue()
    {
        var result = await Run(NewEngine(), "Print(\"before\"); ReturnResult(99); Print(\"after\");");

        Assert.Equal(ResultStatus.Complete, result.Status);
        Assert.Equal(99, result.Value);
        Assert.True(result.ExplicitReturn);
        Assert.Contains("before", result.Stdout);
        Assert.DoesNotContain("after", result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnResultSwallowedByCatch_StillReturnsTheValue()
    {
        var result = await Run(NewEngine(), "try { ReturnResult(5); } catch (Exception) { }\n\"later\"");

        Assert.Equal(5, result.Value);
        Assert.True(result.ExplicitReturn);
    }

    [Fact]
    public async Task ExecuteAsync_TopLevelReturn_IsExplicit()
    {
        var result = await Run(NewEngine(), "return 5;");

        Assert.Equal(5, result.Value);
        Assert.True(result.ExplicitReturn);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnInLambda_IsNotExplicit()
    {
        var result = await Run(NewEngine(), "Func<int> f = () => { return 1; };\nf()");

        Assert.Equal(1, result.Value);
        Assert.False(result.ExplicitReturn);
    }

    [Fact]
    public async Task Out_AfterCellsWithValues_GivesLiveValues()
    {
        var engine = NewEngine();
        await Run(engine, "new List<int> { 1, 2 }");
        await Run(engine, "var skipped = 1;");
        await Run(engine, "\"three\"");

        var viaCell = await Run(engine, "((List<int>)Out[1]).Count + Out[-1].ToString().Length");

        Assert.Equal(7, viaCell.Value);
        Assert.Equal(3, engine.Out.Count);
        Assert.True(engine.Out.Contains(1));
        Assert.False(engine.Out.Contains(2));
        Assert.Throws<KeyNotFoundException>(() => engine.Out[2]);
    }

    [Fact]
    public async Task ExecuteAsync_SelfIsTypedAsTheAgent()
    {
        var agent = new ReplAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() });
        var engine = NewEngine(WithTestAssembly(), agent);

        var result = await Run(engine, "self.Counter = 3; self.Greet(\"bob\")");

        Assert.Equal("hello bob", result.Value);
        Assert.Equal(3, agent.Counter);
    }

    [Fact]
    public async Task DeclareArgumentsAsync_DeclaresTypedVariables()
    {
        var engine = NewEngine();
        await engine.DeclareArgumentsAsync(
            [
                new ReplArgument("items", typeof(List<string>), new List<string> { "a", "bc" }),
                new ReplArgument("count", typeof(int), 2),
                new ReplArgument("event", typeof(Dictionary<string, int[]>), new Dictionary<string, int[]> { ["k"] = [1, 2, 3] }),
                new ReplArgument("maybe", typeof(int?), null),
            ],
            TestContext.Current.CancellationToken);

        var result = await Run(engine, "items.Sum(s => s.Length) + count + @event[\"k\"].Length + (maybe ?? 10)");

        Assert.Equal(18, result.Value);
        Assert.Equal(1, engine.ExecutionCount);
    }

    [Fact]
    public async Task DeclareArgumentsAsync_InvalidNameOrPrivateType_Throws()
    {
        var engine = NewEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.DeclareArgumentsAsync([new ReplArgument("not valid", typeof(int), 1)], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.DeclareArgumentsAsync([new ReplArgument("p", typeof(Hidden), null)], TestContext.Current.CancellationToken));
    }

    private sealed class Hidden;

    [Theory]
    [InlineData("#r \"System.Console.dll\"\n1", 1)]
    [InlineData("var x = 1;\n#load \"a.csx\"", 2)]
    public async Task ExecuteAsync_ReferenceOrLoadDirective_IsRefused(string code, int line)
    {
        var engine = NewEngine();

        var result = await Run(engine, code);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Contains($"line {line}", result.Error);
        Assert.Contains("RestrictedCodeException", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Validator_RunsBeforeCompileAndCanRefuse()
    {
        var validator = new ThrowingCellValidator("forbidden");
        var engine = NewEngine(new ReplOptions { Validator = validator });

        var refused = await Run(engine, "var forbidden = 1;");
        var ok = await Run(engine, "missingName");

        Assert.Contains("RestrictedCodeException: forbidden is forbidden", refused.Error);
        Assert.Equal(ResultStatus.Error, ok.Status);
        Assert.Contains("CS0103", ok.Error);
        Assert.Equal(2, validator.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_UnavailableAssemblies_FailToCompile()
    {
        var engine = NewEngine();

        var console = await Run(engine, "System.Console.WriteLine(1);");
        var process = await Run(engine, "System.Diagnostics.Process.GetCurrentProcess();");

        Assert.Contains("error CS1069", console.Error);
        Assert.Contains("error CS", process.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReadLine_IsForbidden()
    {
        var result = await Run(NewEngine(), "Console.ReadLine()");

        Assert.Contains(ReplConsole.StdinForbidden, result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_DocAndPPrint_WriteToTheCellOutput()
    {
        var result = await Run(NewEngine(WithTestAssembly()), "Doc(typeof(Nooa.Tests.Runtime.ReplHelper)); PPrint(new List<int> { 1, 2 });");

        Assert.Contains("ReplHelper", result.Stdout);
        Assert.Contains("[1, 2]", result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_FencedCode_RunsWithoutTheFence()
    {
        var result = await Run(NewEngine(), "```csharp\nvar x = 2;\nx * 3\n```");

        Assert.Equal(6, result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_FencedCodeError_LinesCountFromTheCode()
    {
        var result = await Run(NewEngine(), "```csharp\nvar a = 1;\nvar b = ;\n```");

        Assert.Contains("line 2", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ReportsErrorAndKeepsPartialOutputAndSession()
    {
        var engine = NewEngine(new ReplOptions { CellTimeout = _wideTimeout });
        await Run(engine, "var kept = 1;");

        var result = await Run(engine, "Print(\"partial\"); await Task.Delay(Timeout.Infinite, Cancellation);");
        var after = await Run(engine, "kept");

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Contains("timed out after 3 seconds", result.Error);
        Assert.Contains("partial", result.Stdout);
        Assert.Equal(1, after.Value);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancel_ReportsCancelledWithPartialOutput()
    {
        var engine = NewEngine();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(_wideTimeout);

        var result = await engine.ExecuteAsync("Print(\"partial\"); await Task.Delay(Timeout.Infinite, Cancellation);", cts.Token);

        Assert.Equal(ResultStatus.Cancelled, result.Status);
        Assert.Contains("partial", result.Stdout);
        Assert.Contains("cancelled", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_LoopThatIgnoresTheToken_IsAbandonedAndSessionIsRefused()
    {
        ReplSpin.Stop = false;
        var engine = NewEngine(WithTestAssembly(new ReplOptions
        {
            CellTimeout = _wideTimeout,
            AbandonGrace = TimeSpan.FromSeconds(1),
        }));
        try
        {
            var result = await Run(engine, "Print(\"spin\"); while (!Nooa.Tests.Runtime.ReplSpin.Stop) { }");

            Assert.Equal(ResultStatus.Error, result.Status);
            Assert.Contains("did not stop", result.Error);
            Assert.Contains("spin", result.Stdout);
            Assert.True(engine.IsAbandoned);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(engine, "1"));
        }
        finally
        {
            ReplSpin.Stop = true;
        }
    }

    [Fact]
    public async Task IsAbandoned_NormalRun_IsFalse()
    {
        var engine = NewEngine();

        await Run(engine, "1");

        Assert.False(engine.IsAbandoned);
    }

    [Fact]
    public async Task TryGetVariable_VariableOfAnEarlierCell_ReturnsTheLatestValue()
    {
        var engine = NewEngine();
        await Run(engine, "var x = 1;");
        await Run(engine, "x = 5;");

        Assert.True(engine.TryGetVariable("x", out var value));
        Assert.Equal(5, value);
    }

    [Fact]
    public async Task TryGetVariable_DeclaredArgument_ReturnsTheValue()
    {
        var engine = NewEngine();
        await engine.DeclareArgumentsAsync([new ReplArgument("items", typeof(List<int>), new List<int> { 1, 2 })], TestContext.Current.CancellationToken);

        Assert.True(engine.TryGetVariable("items", out var value));
        Assert.Equal(new List<int> { 1, 2 }, value);
    }

    [Fact]
    public async Task TryGetVariable_UnknownName_ReturnsFalse()
    {
        var engine = NewEngine();
        await Run(engine, "var x = 1;");

        Assert.False(engine.TryGetVariable("y", out var value));
        Assert.Null(value);
        Assert.False(NewEngine().TryGetVariable("x", out _));
    }

    [Fact]
    public async Task ExecuteAsync_SecondCallWhileRunning_Throws()
    {
        var engine = NewEngine();
        var first = Run(engine, "await Task.Delay(300); 1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(engine, "2"));
        Assert.Equal(1, (await first).Value);
    }
}
