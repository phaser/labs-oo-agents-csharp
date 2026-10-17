// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_loop_guard.py (intent)

using System.Text.Json.Nodes;
using Nooa.Config;
using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class LoopGuardTests
{
    private static LoopGuard Guard(int threshold = 3, int window = 6, int maxOutcome = 500) =>
        new(new LoopGuardConfig { RepeatThreshold = threshold, Window = window, MaxOutcomeChars = maxOutcome });

    private static string Print(JsonNode? args, string name = "execute_csharp") =>
        LoopGuard.Fingerprint(name, args, null, "execute_csharp");

    [Fact]
    public void Fingerprint_CodeWithOtherCommentsAndSpacing_IsTheSame()
    {
        var a = new JsonObject { ["code"] = "var x = 1; // first\nPrint(x);" };
        var b = new JsonObject { ["code"] = "var   x=1;\n/* other */ Print( x );" };

        Assert.Equal(Print(a), Print(b));
    }

    [Fact]
    public void Fingerprint_DifferentCode_Differs()
    {
        Assert.NotEqual(Print(new JsonObject { ["code"] = "Print(1);" }), Print(new JsonObject { ["code"] = "Print(2);" }));
    }

    [Fact]
    public void Fingerprint_CodeThatDoesNotParse_FallsBackToTrimmedLines()
    {
        var a = new JsonObject { ["code"] = "var x = ;\n  Print(x);" };
        var b = new JsonObject { ["code"] = "var x = ;\nPrint(x);  " };

        Assert.Equal(Print(a), Print(b));
    }

    [Fact]
    public void Fingerprint_OtherTool_IgnoresKeyOrder()
    {
        var a = new JsonObject { ["a"] = 1, ["b"] = 2 };
        var b = new JsonObject { ["b"] = 2, ["a"] = 1 };

        Assert.Equal(Print(a, "return_result"), Print(b, "return_result"));
        Assert.NotEqual(Print(a, "return_result"), Print(a, "other"));
    }

    [Fact]
    public void Fingerprint_ArgumentsThatAreNotJson_UseTheRawText()
    {
        Assert.NotEqual(
            LoopGuard.Fingerprint("execute_csharp", null, "[1", "execute_csharp"),
            LoopGuard.Fingerprint("execute_csharp", null, "[2", "execute_csharp"));
    }

    [Fact]
    public void Before_FirstCall_HasNoVerdict()
    {
        Assert.Null(Guard().Before("a"));
    }

    [Fact]
    public void Before_FailedIdenticallyBeforeThresholdMinusOneTimes_Blocks()
    {
        var guard = Guard(threshold: 3);
        foreach (var _ in new[] { 1, 2 })
        {
            Assert.Null(guard.Before("a"));
            Assert.Null(guard.After("a", "boom", failed: true));
        }

        var verdict = guard.Before("a");

        Assert.Equal(new LoopGuardVerdict(LoopGuardAction.Block, 3, "boom"), verdict);
    }

    [Fact]
    public void Before_FailuresWithDifferentOutcomes_DoNotBlock()
    {
        var guard = Guard(threshold: 3);
        guard.Before("a");
        guard.After("a", "one", failed: true);
        guard.Before("a");
        guard.After("a", "two", failed: true);

        Assert.Null(guard.Before("a"));
    }

    [Fact]
    public void Before_ProposedAgainAfterABlock_Stops()
    {
        var guard = Guard(threshold: 2);
        guard.Before("a");
        guard.After("a", "boom", failed: true);
        Assert.Equal(LoopGuardAction.Block, guard.Before("a")!.Action);

        var verdict = guard.Before("a");

        Assert.Equal(LoopGuardAction.Stop, verdict!.Action);
    }

    [Fact]
    public void Before_ProposedAgainAfterTheWindow_RunsAgain()
    {
        var guard = Guard(threshold: 2, window: 2);
        guard.Before("a");
        guard.After("a", "boom", failed: true);
        Assert.Equal(LoopGuardAction.Block, guard.Before("a")!.Action);
        guard.Before("b");
        guard.After("b", "ok", failed: false);
        guard.Before("c");
        guard.After("c", "ok", failed: false);

        Assert.Null(guard.Before("a"));
    }

    [Fact]
    public void After_IdenticalSuccessAtTheThreshold_Nudges()
    {
        var guard = Guard(threshold: 3);
        Assert.Null(guard.After("a", "same", failed: false));
        Assert.Null(guard.After("a", "same", failed: false));

        var verdict = guard.After("a", "same", failed: false);

        Assert.Equal(new LoopGuardVerdict(LoopGuardAction.Nudge, 3), verdict);
    }

    [Fact]
    public void After_RepeatAfterANudge_Stops()
    {
        var guard = Guard(threshold: 2);
        guard.Before("a");
        guard.After("a", "same", failed: false);
        guard.Before("a");
        Assert.Equal(LoopGuardAction.Nudge, guard.After("a", "same", failed: false)!.Action);
        guard.Before("a");

        Assert.Equal(LoopGuardAction.Stop, guard.After("a", "same", failed: false)!.Action);
    }

    [Fact]
    public void After_DifferentOutputs_NeverNudge()
    {
        var guard = Guard(threshold: 2);

        Assert.Null(guard.After("a", "1", failed: false));
        Assert.Null(guard.After("a", "2", failed: false));
        Assert.Null(guard.After("a", "3", failed: false));
    }

    [Fact]
    public void Message_Block_QuotesTheOutcomeAndBoundsIt()
    {
        var guard = Guard(maxOutcome: 5);

        var text = guard.Message(new LoopGuardVerdict(LoopGuardAction.Block, 3, "0123456789"), "execute_csharp", completion: false);

        Assert.StartsWith("Loop guard: this execute_csharp call was not executed.", text, StringComparison.Ordinal);
        Assert.Contains("already failed 2 times in the last 6 tool calls", text, StringComparison.Ordinal);
        Assert.Contains("01234...", text, StringComparison.Ordinal);
        Assert.DoesNotContain("56789", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_NudgeAndStop_UseTheirTemplates()
    {
        var guard = Guard();

        Assert.Contains("ran 3 times in the last 6 tool calls with identical output", guard.Message(new LoopGuardVerdict(LoopGuardAction.Nudge, 3), "execute_csharp", false), StringComparison.Ordinal);
        Assert.StartsWith("Loop guard stopped generation", guard.Message(new LoopGuardVerdict(LoopGuardAction.Stop, 4), "execute_csharp", false), StringComparison.Ordinal);
    }

    [Fact]
    public void Message_Completion_AddsTheReturnResultHint()
    {
        var text = Guard().Message(new LoopGuardVerdict(LoopGuardAction.Nudge, 3), "execute_csharp", completion: true);

        Assert.EndsWith("then call ReturnResult(variable).", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MentionsReturnResult_LooksAtTheCodeArgument()
    {
        Assert.True(LoopGuard.MentionsReturnResult(new JsonObject { ["code"] = "ReturnResult(1);" }));
        Assert.False(LoopGuard.MentionsReturnResult(new JsonObject { ["code"] = "Print(1);" }));
        Assert.False(LoopGuard.MentionsReturnResult(new JsonObject { ["other"] = "ReturnResult" }));
        Assert.False(LoopGuard.MentionsReturnResult(null));
    }

    [Fact]
    public void NormalizeCode_IgnoresCommentsAndWhitespace()
    {
        Assert.Equal(LoopGuard.NormalizeCode("var a=1;//c"), LoopGuard.NormalizeCode("var   a = 1 ;\n"));
    }
}
