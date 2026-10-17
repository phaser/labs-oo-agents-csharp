// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/05_progressive_disclosure.py, 07_dynamic_prompts.py and 08_context_blocks.py

using Nooa.ContextBlocks;
using Nooa.Llm;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

/// <summary>Quickstart 05, 07 and 08 as offline tests: agent classes compiled by the generator, scripted model answers, CodeAct as the default.</summary>
public class ContextAcceptanceTests
{
    private static IReadOnlyList<string> Contents(FakeLlmCall call) =>
        [.. call.Messages.OfType<JsonMessage>().Select(m => m.Value["content"]?.GetValue<string>() ?? "")];

    private static string All(FakeLlmCall call) => string.Join('\n', Contents(call));

    [Fact]
    public async Task Quickstart05_ModelExploresAnUnknownTypeWithDoc_ThenReadsTheValue()
    {
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "var item = self.GetItem(itemId); Doc(item!);" }, id: "call_1")
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(((Artwork)item!).GetAppraisal().Value);" }, id: "call_2");
        var appraiser = new WarehouseAppraiser(new AgentOptions { Llm = fake });

        var value = await appraiser.AppraiseItemAsync("ART-001");

        Assert.Equal(15000.0, value);
        Assert.Equal(2, fake.CallCount);
        var second = All(fake.Calls[1]);
        Assert.Contains("GetAppraisal", second, StringComparison.Ordinal);
        Assert.Contains("Appraisal GetAppraisal();", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart07_DocCommentPlaceholder_IsFilledFromTheInstance()
    {
        var spanish = new FakeLlmClient().EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Hola, como estas hoy?\");" });
        var german = new FakeLlmClient().EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Hallo, wie geht es dir heute?\");" });
        var spanishFormal = new FakeLlmClient().EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Hola, como esta usted hoy?\");" });
        var agentEs = new TranslatorAgent("Spanish", new AgentOptions { Llm = spanish });
        var agentDe = new TranslatorAgent("German", new AgentOptions { Llm = german });

        var es = await agentEs.TranslateAsync("Hello, how are you today?");
        var de = await agentDe.TranslateAsync("Hello, how are you today?");
        agentEs.SetLlm(spanishFormal);
        var formal = await agentEs.TranslateFormalAsync("Hello, how are you today?");

        Assert.Equal("Hola, como estas hoy?", es);
        Assert.Equal("Hallo, wie geht es dir heute?", de);
        Assert.Equal("Hola, como esta usted hoy?", formal);
        Assert.Contains("Translate the text to Spanish.", All(spanish.Calls[0]), StringComparison.Ordinal);
        Assert.Contains("Translate the text to German.", All(german.Calls[0]), StringComparison.Ordinal);
        Assert.DoesNotContain("Translate the text to Spanish.", All(german.Calls[0]), StringComparison.Ordinal);
        Assert.Contains("Translate the text to Spanish using formal register.", All(spanishFormal.Calls[0]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart08_ExpressionBlock_ShowsTheLatestNotesEveryTurn()
    {
        string[] notes =
        [
            "Deploy uses blue-green strategy with 5-minute health checks.",
            "Database migrations run before traffic shifts.",
            "Rollback is automatic if error rate exceeds 1% for 2 minutes.",
        ];
        var fake = new FakeLlmClient();
        foreach (var note in notes)
        {
            fake.EnqueueToolCall("execute_csharp", new { code = "self.AddNote(note); ReturnResult(\"Saved: \" + note);" });
        }

        fake.EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Automatic rollback if the error rate exceeds 1% for 2 minutes.\");" });
        fake.EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Roll back.\");" });
        var agent = new NoteTakingAgent(new AgentOptions { Llm = fake });
        agent.Context["notes"] = Context.Expr("self.RenderNotes()");

        foreach (var note in notes)
        {
            await agent.RecordAsync(note);
        }

        var answer = await agent.AnswerAsync("What triggers an automatic rollback?");
        agent.Context["policy"] = Context.Of("Always prefer rollback over forward-fix during incidents.", prefix: true);
        var answer2 = await agent.AnswerAsync("Should we try to fix forward or roll back?");

        Assert.StartsWith("Automatic rollback", answer, StringComparison.Ordinal);
        Assert.Equal("Roll back.", answer2);
        Assert.Contains("No notes yet.", All(fake.Calls[0]), StringComparison.Ordinal);
        var firstAnswer = All(fake.Calls[3]);
        foreach (var note in notes)
        {
            Assert.Contains($"- {note}", firstAnswer, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Always prefer rollback", firstAnswer, StringComparison.Ordinal);
        var secondAnswer = Contents(fake.Calls[4]);
        Assert.Contains("<policy>\nAlways prefer rollback over forward-fix during incidents.\n</policy>", secondAnswer[0], StringComparison.Ordinal);
        Assert.Contains("ContextApi", secondAnswer[0], StringComparison.Ordinal);
    }
}
