// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_error_formatting.py, tests/test_error_line_numbers.py (intent; Python traceback specifics do not apply)

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class ErrorFormattingTests
{
    private static IReadOnlyList<Diagnostic> Compile(string code) =>
        CSharpScript.Create(code, ScriptOptions.Default.WithFilePath("Cell In[4]").WithImports("System"))
            .Compile(TestContext.Current.CancellationToken);

    [Fact]
    public void FormatForLlm_CompileError_ShowsLocationSourceAndMarker()
    {
        var text = ErrorFormatting.FormatDiagnostics(Compile("var a = 1;\nvar b = a +;"));

        Assert.Equal(
            "Cell In[4], line 2\n  var b = a +;\n             ^\nerror CS1525: Invalid expression term ';'",
            text);
    }

    [Fact]
    public void FormatForLlm_ManyCompileErrors_ListsTenAndCountsTheRest()
    {
        var code = string.Join("\n", Enumerable.Range(0, 14).Select(i => $"undefined{i}();"));

        var text = ErrorFormatting.FormatDiagnostics(Compile(code));

        Assert.Contains("line 10", text);
        Assert.DoesNotContain("line 11", text);
        Assert.EndsWith("... and 4 more errors", text);
    }

    [Fact]
    public void FormatDiagnostics_OnlyWarnings_SaysCompilationFailed()
    {
        Assert.Equal("Compilation failed.", ErrorFormatting.FormatDiagnostics([]));
    }

    [Fact]
    public void FormatForLlm_RestrictedCodeWithLine_ShowsLineAndType()
    {
        var text = ErrorFormatting.FormatForLlm(new RestrictedCodeException("no files", 3));

        Assert.Equal("line 3\nRestrictedCodeException: no files", text);
    }

    [Fact]
    public void FormatForLlm_ValidationWithoutLine_ShowsTypeAndMessage()
    {
        Assert.Equal("ValidationException: bad", ErrorFormatting.FormatForLlm(new ValidationException("bad")));
    }

    [Fact]
    public void FormatForLlm_PlainException_ShowsTypeAndMessageWithoutFrames()
    {
        Exception error;
        try
        {
            throw new ArgumentException("nope");
        }
        catch (Exception e)
        {
            error = e;
        }

        var text = ErrorFormatting.FormatForLlm(error);

        Assert.Equal("ArgumentException: nope", text);
        Assert.DoesNotContain("FormatForLlm", text);
        Assert.DoesNotContain("/", text);
    }

    [Fact]
    public void FormatForLlm_InnerException_ShowsCauseFirst()
    {
        var text = ErrorFormatting.FormatForLlm(new InvalidOperationException("outer", new FormatException("inner")));

        Assert.Equal(
            "FormatException: inner\n\nThe above exception was the direct cause of the following exception:\n\nInvalidOperationException: outer",
            text);
    }

    [Fact]
    public void FormatForLlm_SingleWrapper_IsUnwrapped()
    {
        var text = ErrorFormatting.FormatForLlm(new AggregateException(new System.Reflection.TargetInvocationException(new FormatException("deep"))));

        Assert.Equal("FormatException: deep", text);
    }

    [Fact]
    public void FormatForLlm_AggregateWithManyErrors_ShowsEach()
    {
        var text = ErrorFormatting.FormatForLlm(new AggregateException(new FormatException("one"), new ArgumentException("two")));

        Assert.Contains("FormatException: one", text);
        Assert.Contains("ArgumentException: two", text);
    }

    [Fact]
    public void FormatForLlm_LongMessage_IsCutToTheBudget()
    {
        var text = ErrorFormatting.FormatForLlm(new InvalidOperationException(new string('x', 5_000)), maxError: 200);

        Assert.True(text.Length < 500);
        Assert.Contains("<truncated-output>", text);
        Assert.Contains("InvalidOperationException: xxx", text);
    }

    [Fact]
    public void FormatForLlm_InvalidBudget_FallsBackToTheDefault()
    {
        var text = ErrorFormatting.FormatForLlm(new InvalidOperationException("short"), maxError: -5, tailChars: 99_999);

        Assert.Equal("InvalidOperationException: short", text);
    }

    [Fact]
    public void FormatForLlm_HostileMessage_FallsBackToTheTypeName()
    {
        Assert.Equal("HostileException", ErrorFormatting.FormatForLlm(new HostileException()));
    }

    [Fact]
    public void FormatForLlm_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ErrorFormatting.FormatForLlm(null!));
    }

    private sealed class HostileException : Exception
    {
        public override string Message => throw new InvalidOperationException("hostile");
    }
}
