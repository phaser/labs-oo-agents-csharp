// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_response_cleanup.py

using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class ResponseCleanupTests
{
    [Theory]
    [InlineData("```csharp\nvar x = 1;\n```", "var x = 1;", "```csharp")]
    [InlineData("```cs\nvar x = 1;\n```", "var x = 1;", "```cs")]
    [InlineData("```c#\nvar x = 1;\n```", "var x = 1;", "```c#")]
    [InlineData("```\nvar x = 1;\n```", "var x = 1;", "```")]
    [InlineData("```bash\necho hello\n```", "echo hello", "```bash")]
    [InlineData("  ```csharp\nvar x = 1;\n```  ", "var x = 1;", "```csharp")]
    [InlineData("```csharp Print(1);\n```", "Print(1);", "```csharp")]
    [InlineData("```csharp var x = 1;```", "var x = 1;", "```csharp")]
    [InlineData("```csharp\r\nvar x = 1;\r\n```", "var x = 1;", "```csharp")]
    public void StripCodeFences_Fenced_ReturnsCodeAndFence(string input, string expectedCode, string expectedFence)
    {
        var (code, fence) = ResponseCleanup.StripCodeFences(input);

        Assert.Equal(expectedCode, code);
        Assert.Equal(expectedFence, fence);
    }

    [Theory]
    [InlineData("var x = 1;\nvar y = 2;")]
    [InlineData("```csharp\nvar x = 1;")]
    [InlineData("var x = 1;\n```")]
    public void StripCodeFences_NoBalancedFence_ReturnsOriginal(string input)
    {
        var (code, fence) = ResponseCleanup.StripCodeFences(input);

        Assert.Equal(input, code);
        Assert.Null(fence);
    }

    [Fact]
    public void StripCodeFences_MultilineCode_KeepsAllLines()
    {
        var (code, _) = ResponseCleanup.StripCodeFences("```csharp\nusing System;\nvar x = 1;\nPrint(x);\n```");

        Assert.Equal("using System;\nvar x = 1;\nPrint(x);", code);
    }

    [Fact]
    public void StripCodeFences_CodeOnOpeningLineWithMoreLines_KeepsBoth()
    {
        var (code, fence) = ResponseCleanup.StripCodeFences("```csharp var x = 1;\nvar y = 2;\n```");

        Assert.Equal("var x = 1;\nvar y = 2;", code);
        Assert.Equal("```csharp", fence);
    }

    [Fact]
    public void StripCodeFences_Applied_IsIdempotent()
    {
        var (once, _) = ResponseCleanup.StripCodeFences("var x = 1;");
        var (twice, _) = ResponseCleanup.StripCodeFences(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void StripCodeFences_JunkAfterLanguageTag_KeepsTheCode()
    {
        var (code, fence) = ResponseCleanup.StripCodeFences("```csharp 3\nPrint(1);\n```");

        Assert.Equal("```csharp", fence);
        Assert.Contains("Print(1);", code);
    }

    [Fact]
    public void StripCodeFences_HostileLongInput_FinishesQuickly()
    {
        var input = "```" + new string('`', 20_000) + "\n" + new string('a', 20_000);

        var (code, fence) = ResponseCleanup.StripCodeFences(input);

        Assert.Equal(input, code);
        Assert.Null(fence);
    }
}
