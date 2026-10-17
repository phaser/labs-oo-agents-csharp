// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_code_validator.py, tests/runtime/test_blocking_call_validator.py, tests/runtime/test_restricted_imports.py

using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class CellCodeValidatorTests
{
    private static CellCodeValidator NewValidator(CellValidatorOptions? options = null) =>
        new((options ?? new CellValidatorOptions()) with { GlobalsType = typeof(ReplGlobals<ReplAgent>) });

    private static RestrictedCodeException Reject(string code, CellValidatorOptions? options = null) =>
        Assert.Throws<RestrictedCodeException>(() => NewValidator(options).Validate(code));

    [Theory]
    [InlineData("1 + 2")]
    [InlineData("var xs = new[] { 3, 1, 2 }.OrderBy(x => x).ToList(); Print(xs.Count);")]
    [InlineData("await Task.Delay(1); Print(Environment.NewLine);")]
    [InlineData("_ = Task.Delay(1);")]
    [InlineData("var t = Task.Delay(1); await t;")]
    [InlineData("int Twice(int x) => x * 2; Twice(4)")]
    [InlineData("var p = System.IO.Path.Combine(\"a\", \"b\");")]
    [InlineData("await self.SlowAsync()")]
    [InlineData("var r = new[] { 1, 2 }.Select(x => x + 1).Sum();")]
    [InlineData("")]
    public void Validate_PlainCode_Passes(string code)
    {
        NewValidator().Validate(code);
    }

    [Theory]
    [InlineData("File.ReadAllText(\"a\")", "System.IO.File")]
    [InlineData("System.IO.File.ReadAllText(\"a\")", "System.IO.File")]
    [InlineData("global::System.IO.File.Delete(\"a\")", "System.IO.File")]
    [InlineData("var f = System.IO.File.Exists(\"a\");", "System.IO.File")]
    [InlineData("System.IO.StreamReader r = new(\"a\");", "StreamReader/StreamWriter")]
    [InlineData("System.IO.Directory.GetFiles(\".\")", "System.IO.Directory")]
    [InlineData("new System.IO.FileInfo(\"a\")", "System.IO.FileInfo")]
    [InlineData("new System.IO.StreamReader(\"a\")", "StreamReader/StreamWriter")]
    [InlineData("new System.IO.StreamWriter(\"a\")", "StreamReader/StreamWriter")]
    [InlineData("Environment.Exit(1)", "Environment.Exit")]
    [InlineData("Environment.GetEnvironmentVariable(\"HOME\")", "System.Environment")]
    [InlineData("Environment.CurrentDirectory", "System.Environment")]
    [InlineData("typeof(string).Assembly", "System.Type")]
    [InlineData("\"x\".GetType()", "Object.GetType")]
    [InlineData("System.Reflection.Assembly.Load(\"x\")", "System.Reflection")]
    [InlineData("Activator.CreateInstance(typeof(object))", "System.Activator")]
    [InlineData("AppDomain.CurrentDomain", "System.AppDomain")]
    [InlineData("System.Runtime.InteropServices.Marshal.AllocHGlobal(1)", "System.Runtime.InteropServices")]
    [InlineData("System.Runtime.CompilerServices.Unsafe.SizeOf<int>()", "System.Runtime.CompilerServices.Unsafe")]
    [InlineData("Process.Start(\"ls\")", "System.Diagnostics.Process")]
    [InlineData("System.Console.ReadLine()", "System.Console")]
    [InlineData("System.Diagnostics.Debugger.Break()", "System.Diagnostics.Debugger")]
    [InlineData("self.GetType()", "Object.GetType")]
    public void Validate_RestrictedApi_Rejects(string code, string expected)
    {
        var error = Reject(code);

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void Validate_AliasAndUsingStatic_Reject()
    {
        Assert.Contains("System.IO.File", Reject("using F = System.IO.File;\nF.ReadAllText(\"a\")").Message, StringComparison.Ordinal);
        Assert.Contains("System.IO.File", Reject("using static System.IO.File;\nReadAllText(\"a\")").Message, StringComparison.Ordinal);
        Assert.Contains("System.Reflection", Reject("using System.Reflection;\n1").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ImportAcceptedInEarlierCell_StillRejectsTypeInLaterCell()
    {
        var validator = NewValidator();
        validator.Validate("using System.IO;");

        var error = Assert.Throws<RestrictedCodeException>(() => validator.Validate("File.ReadAllText(\"a\")"));

        Assert.Contains("System.IO.File", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dynamic d = 1;", "dynamic")]
    [InlineData("unsafe { int x = 1; }", "unsafe")]
    [InlineData("[System.Runtime.InteropServices.DllImport(\"libc\")] static extern int getpid();", "extern")]
    [InlineData("int* p = null;", "Unsafe code")]
    public void Validate_DynamicUnsafeExtern_Rejects(string code, string expected)
    {
        Assert.Contains(expected, Reject(code).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("System.Threading.Thread.Sleep(1);", "Thread.Sleep")]
    [InlineData("var t = Task.FromResult(1); var r = t.Result;", "Task.Result")]
    [InlineData("Task.Delay(1).Wait();", "Task.Wait")]
    [InlineData("Task.WaitAll(Task.Delay(1));", "Task.WaitAll")]
    [InlineData("Task.WaitAny(Task.Delay(1));", "Task.WaitAny")]
    [InlineData("var r = Task.FromResult(1).GetAwaiter().GetResult();", "GetResult")]
    [InlineData("new SemaphoreSlim(1).Wait();", "SemaphoreSlim.Wait")]
    [InlineData("new ManualResetEvent(true).WaitOne();", "WaitHandle.WaitOne")]
    public void Validate_BlockingCall_Rejects(string code, string expected)
    {
        var error = Reject(code);

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Contains("await", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ResultOfTaskFromEarlierCell_Rejects()
    {
        var validator = NewValidator();
        validator.Validate("var t = Task.FromResult(1);");

        var error = Assert.Throws<RestrictedCodeException>(() => validator.Validate("t.Result"));

        Assert.Contains("Task.Result", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectedCell_IsNotChained()
    {
        var validator = NewValidator();
        Assert.Throws<RestrictedCodeException>(() => validator.Validate("var u = Task.FromResult(1); System.Threading.Thread.Sleep(1);"));

        validator.Validate("var u = 5;");
        validator.Validate("u + 1");
    }

    [Theory]
    [InlineData("Task.Delay(1);")]
    [InlineData("self.SlowAsync();")]
    [InlineData("Task.Run(() => 1);")]
    public void Validate_UnawaitedTask_Rejects(string code)
    {
        var error = Reject(code);

        Assert.Contains("Add await", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PragmaDisableOfUnawaitedWarning_Rejects()
    {
        var error = Reject("#pragma warning disable CS4014\nTask.Delay(1);");

        Assert.Contains("disable warnings", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class Foo {}")]
    [InlineData("struct Foo {}")]
    [InlineData("record Foo(int X);")]
    [InlineData("enum Color { Red }")]
    [InlineData("interface IFoo {}")]
    [InlineData("delegate void Cb();")]
    public void Validate_TypeDeclaration_Rejects(string code)
    {
        Assert.Contains("must not declare a type", Reject(code).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SelfCallOfForbiddenMethod_Rejects()
    {
        var options = new CellValidatorOptions { ForbiddenSelfCalls = ["Greet"] };

        var error = Reject("var x = 1;\nself.Greet(\"a\")", options);

        Assert.Equal(2, error.LineNumber);
        Assert.Contains("infinite recursion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SelfCallOfOtherMethod_Passes()
    {
        NewValidator(new CellValidatorOptions { ForbiddenSelfCalls = ["Greet"] }).Validate("self.Counter");
    }

    [Theory]
    [InlineData("var Answer = 1;")]
    [InlineData("int Answer() => 1;")]
    [InlineData("if (Out is var Answer) { }")]
    [InlineData("using Answer = System.String;")]
    [InlineData("var ReplAgent = 1;")]
    public void Validate_NameOfReturnType_Rejects(string code)
    {
        var error = Reject(code, new CellValidatorOptions { ReturnType = typeof(Answer<ReplAgent>) });

        Assert.Contains("return type", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NameOfOtherType_Passes()
    {
        NewValidator(new CellValidatorOptions { ReturnType = typeof(Answer<int>) }).Validate("var Value = 1;");
    }

    [Fact]
    public void Validate_AllowedGroup_PermitsItsApisOnly()
    {
        var options = new CellValidatorOptions { Allow = RestrictedApiGroup.FileSystem };

        NewValidator(options).Validate("System.IO.File.Exists(\"a\")");
        Reject("System.Reflection.Assembly.Load(\"x\")", options);
        Reject("System.Threading.Thread.Sleep(1);", options with { Allow = (RestrictedApiGroup)255 });
    }

    [Fact]
    public void Validate_AllowedReflection_PermitsDynamicAndType()
    {
        NewValidator(new CellValidatorOptions { Allow = RestrictedApiGroup.Reflection }).Validate("dynamic d = 1; typeof(string).Name");
    }

    [Fact]
    public void Validate_SeveralIssues_ListsEachWithLineAndMarker()
    {
        var error = Reject("var a = 1;\nSystem.IO.File.Delete(\"a\");\nThread.Sleep(1);");

        Assert.Equal(2, error.LineNumber);
        Assert.Contains("breaks 2 rules", error.Message, StringComparison.Ordinal);
        Assert.Contains("line 2\n  System.IO.File.Delete(\"a\");\n", error.Message, StringComparison.Ordinal);
        Assert.Contains("line 3\n  Thread.Sleep(1);\n  ^^^^^^^^^^^^\n", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Engine_WithValidator_RefusesRestrictedCellAndKeepsState()
    {
        var globals = new ReplGlobals<ReplAgent>(new ReplAgent(new AgentOptions { Llm = new Nooa.Llm.FakeLlmClient() }));
        var options = new ReplOptions { Validator = new CellCodeValidator(new CellValidatorOptions { GlobalsType = globals.GetType() }) };
        var engine = new RoslynExecutionEngine(globals, options);
        var token = TestContext.Current.CancellationToken;

        await engine.ExecuteAsync("var x = 41;", token);
        var refused = await engine.ExecuteAsync("System.IO.File.ReadAllText(\"/etc/hosts\")", token);
        var after = await engine.ExecuteAsync("x + 1", token);

        Assert.Equal(ResultStatus.Error, refused.Status);
        Assert.Contains("System.IO.File is restricted", refused.Error, StringComparison.Ordinal);
        Assert.Equal(42, after.Value);
    }
}
