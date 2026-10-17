// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/01_first_generation_method.py to 08_context_blocks.py

using System.Text.Json.Nodes;
using Nooa.Examples.Quickstart;
using Nooa.Examples.Quickstart.Example01;
using Nooa.Examples.Quickstart.Example02;
using Nooa.Examples.Quickstart.Example03;
using Nooa.Examples.Quickstart.Example04;
using Nooa.Examples.Quickstart.Example05;
using Nooa.Examples.Quickstart.Example06;
using Nooa.Examples.Quickstart.Example07;
using Nooa.Examples.Quickstart.Example08;
using Nooa.Llm;
using Nooa.Tracing;

namespace Nooa.Tests.Acceptance;

/// <summary>
/// The v1 acceptance suite: quickstart examples 01 to 08, run offline.
/// Each test runs the real example class from <c>examples/Quickstart</c> against a <see cref="FakeLlmClient"/> with scripted answers.
/// The agent classes are compiled by the source generator, so the whole path is covered except the network.
/// CodeAct is the default strategy, as upstream.
/// </summary>
[Collection(AgentStaticCollection.Name)]
public sealed class QuickstartAcceptanceTests : IDisposable
{
    private const string _inventoryCell =
        """
        var total = 0.0;
        var unavailable = new List<string>();
        foreach (var item in items)
        {
            if (self.GetStock(item) > 0) total += self.GetPrice(item);
            else unavailable.Add(item);
        }
        ReturnResult(new OrderResult(unavailable.Count == 0 && total <= budget, total, unavailable));
        """;

    // Tracing is process-wide state. The collection serializes the tests that change it.
    public QuickstartAcceptanceTests() => NooaTracing.Shutdown();

    public void Dispose() => NooaTracing.Shutdown();

    private static IReadOnlyList<string> Contents(FakeLlmCall call) =>
        [.. call.Messages.OfType<JsonMessage>().Select(m => m.Value["content"]?.GetValue<string>() ?? "")];

    private static string All(FakeLlmCall call) => string.Join('\n', Contents(call));

    private static async Task<string> Run(IQuickstartExample example, FakeLlmClient fake)
    {
        var output = new StringWriter();
        await example.RunAsync(fake, output);
        return output.ToString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void CreateAll_ListsTheEightExamplesInOrder()
    {
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], QuickstartExamples.CreateAll().Select(e => e.Number));
    }

    [Fact]
    public async Task Quickstart01_FirstGenerationMethod_ReturnsTheModelAnswerWithPromptsFromTheDocComments()
    {
        var fake = new FakeLlmClient().EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Positive tone, but slow shipping.\");" });

        var output = await Run(new FirstGenerationMethodExample(), fake);

        Assert.Equal("Positive tone, but slow shipping.\n", output);
        var call = Assert.Single(fake.Calls);
        var system = Contents(call)[0];
        Assert.Contains("<system_prompt>\nYou are an agent specializing in analyzing customer feedback.\n</system_prompt>", system, StringComparison.Ordinal);
        var all = All(call);
        Assert.Contains("Analyze customer feedback for sentiment and key topics in one sentence.", all, StringComparison.Ordinal);
        Assert.Contains("Great product, but shipping was slow", all, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart02_StructuredOutput_ReturnsATypedResult()
    {
        var fake = new FakeLlmClient().EnqueueText(
            """
            {"sentiment": "negative", "topics": ["feature"], "urgency": "high", "summary": "A feature is broken.", "confidence": 0.9}
            """);

        var output = await Run(new StructuredOutputExample(), fake);

        Assert.Equal("Sentiment: negative\nTopics: feature\nUrgency: high\nSummary: A feature is broken.\nConfidence: 0.9\n", output);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(typeof(FeedbackAnalysis), call.OutputType);
        var contents = Contents(call);
        Assert.Contains("Agent for analyzing customer feedback with structured output.", contents[0], StringComparison.Ordinal);
        Assert.Contains(contents, c => c.Contains("# Your task\nAnalyze customer feedback comprehensively.", StringComparison.Ordinal) && c.Contains("Broken feature, needs immediate fix!", StringComparison.Ordinal));
        Assert.Contains(contents, c => c.Contains("<strategy_prompt>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quickstart02_InvalidThenValidAnswer_RetriesOnce()
    {
        var fake = new FakeLlmClient()
            .EnqueueText("""{"sentiment": "negative", "topics": "feature", "urgency": "high", "summary": "s", "confidence": 0.9}""")
            .EnqueueText("""{"sentiment": "negative", "topics": ["feature"], "urgency": "high", "summary": "s", "confidence": 0.9}""");

        var output = await Run(new StructuredOutputExample(), fake);

        Assert.Contains("Topics: feature\n", output, StringComparison.Ordinal);
        Assert.Equal(2, fake.CallCount);
        Assert.Contains(Contents(fake.Calls[1]), c => c.Contains("Field `$.topics`", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quickstart03_CodeActIsTheDefault_RunsACellAndReturnsTheTypedResult()
    {
        var fake = new FakeLlmClient().EnqueueToolCall("execute_csharp", new { code = _inventoryCell });

        var output = await Run(new CodeActToolsExample(), fake);

        Assert.Equal("Can fulfill: False\nTotal cost: 1.25\nUnavailable items: orange\n", output);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(["execute_csharp", "return_result"], call.Tools!.Select(t => t.Name));
        Assert.Contains("You are an agent that checks inventory", Contents(call)[0], StringComparison.Ordinal);
        Assert.Contains("Jupyter-like C# session", Contents(call)[0], StringComparison.Ordinal);
        Assert.Contains("GetStock", Contents(call)[0], StringComparison.Ordinal);
        var all = All(call);
        Assert.Contains("Task: CanFulfillOrder()", all, StringComparison.Ordinal);
        Assert.Contains("items (List<string>):", all, StringComparison.Ordinal);
        Assert.Contains("record OrderResult", all, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart03_ModelCallsAMethodFirst_SeesTheOutputOnTheNextTurn()
    {
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "Print(self.GetStock(\"apple\"));" }, id: "call_1")
            .EnqueueToolCall("execute_csharp", new { code = _inventoryCell }, id: "call_2");

        await Run(new CodeActToolsExample(), fake);

        Assert.Equal(2, fake.CallCount);
        Assert.Contains(Contents(fake.Calls[1]), c => c.Contains("50", StringComparison.Ordinal) && c.Contains("CellOutput", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quickstart04_PredictThenCodeAct_ClassifiesAndRunsTheShell()
    {
        using var directory = new TempDirectory();
        var name = Path.GetFileName(directory.Path);
        File.WriteAllText(Path.Combine(directory.Path, "marker.txt"), "x");
        var fake = new FakeLlmClient()
            .EnqueueText("""{"result": "positive"}""")
            .EnqueueToolCall("execute_csharp", new { code = "var r = await self.Shell.RunAsync(\"pwd\"); ReturnResult(r.Stdout.Trim());" })
            .EnqueueToolCall("execute_csharp", new { code = "var r = await self.Shell.RunAsync(\"ls\"); ReturnResult(r.Stdout.Trim());" });

        var output = await Run(new StrategiesExample(directory.Path), fake);

        Assert.Contains("Sentiment: positive\n", output, StringComparison.Ordinal);
        Assert.Contains("Request: What is the current working directory?\nResponse: ", output, StringComparison.Ordinal);
        Assert.Contains(name, output, StringComparison.Ordinal);
        Assert.Contains("Response: marker.txt\n", output, StringComparison.Ordinal);
        Assert.Equal(["execute_csharp", "return_result"], fake.Calls[1].Tools!.Select(t => t.Name));
    }

    [Fact]
    public async Task Quickstart05_ModelExploresAnUnknownTypeWithDoc_ThenReadsTheValue()
    {
        var fake = new FakeLlmClient();
        foreach (var itemId in new[] { "ART-001", "STK-001", "JWL-001", "COL-001" })
        {
            fake.EnqueueToolCall("execute_csharp", new { code = "var item = self.GetItem(itemId); Doc(item!);" })
                .EnqueueToolCall("execute_csharp", new { code = ValueCell(itemId) });
        }

        var output = await Run(new ProgressiveDisclosureExample(), fake);

        Assert.Equal(
            "  ART-001 (Artwork): $15,000.00\n  STK-001 (StockHolding): $87,550.00\n  JWL-001 (Jewelry): $20,000.00\n  COL-001 (Collectible): $4,250.00\n",
            output);
        var second = All(fake.Calls[1]);
        Assert.Contains("GetAppraisal", second, StringComparison.Ordinal);
        Assert.Contains("Appraisal GetAppraisal();", second, StringComparison.Ordinal);
        Assert.Contains("ComputeValue", All(fake.Calls[5]), StringComparison.Ordinal);
    }

    private static string ValueCell(string itemId) => itemId switch
    {
        "ART-001" => "ReturnResult(((Artwork)item!).GetAppraisal().Value);",
        "STK-001" => "ReturnResult(((StockHolding)item!).GetTotalValue());",
        "JWL-001" => "ReturnResult(((Jewelry)item!).ComputeValue());",
        _ => "ReturnResult(((Collectible)item!).EstimateValue());",
    };

    [Fact]
    public async Task Quickstart06_TracedRun_WritesTheParentChildSpanTree()
    {
        using var dir = new TempDirectory();
        var example = new TracingExample(dir.Path);
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(30.0);" })
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"(10 + 5) * 2 is 30 because the sum is doubled.\");" });

        var output = await Run(example, fake);

        Assert.StartsWith("Result: (10 + 5) * 2 is 30 because the sum is doubled.\n", output, StringComparison.Ordinal);
        Assert.Contains(example.TraceFile, output, StringComparison.Ordinal);
        var spans = ReadSpans(example.TraceFile);
        var byId = spans.ToDictionary(s => (string)s["spanId"]!);
        string Parent(JsonNode s) => (string?)s["parentSpanId"] is { } id ? (string)byId[id]["name"]! : "";
        var run = Assert.Single(spans, s => (string)s["name"]! == "method.RunAsync");
        var calculate = Assert.Single(spans, s => (string)s["name"]! == "method.CalculateAsync");
        var explain = Assert.Single(spans, s => (string)s["name"]! == "method.ExplainAsync");
        Assert.Equal("", Parent(run));
        Assert.Equal("method.RunAsync", Parent(calculate));
        Assert.Equal("method.RunAsync", Parent(explain));
        Assert.Single(spans.Select(s => (string)s["traceId"]!).Distinct());
        Assert.Equal(3, spans.Count(s => (string)s["name"]! == "generation"));
        Assert.Equal(2, spans.Count(s => (string)s["name"]! == "llm"));
        Assert.All(spans.Where(s => (string)s["name"]! == "llm"), s => Assert.Equal("generation", Parent(s)));
        Assert.All(spans, s => Assert.Equal(TracingExample.SessionId, Attr(s, "session.id")));
        Assert.Equal("AGENT", Attr(run, "openinference.span.kind"));
        Assert.Equal("LLM", Attr(spans.First(s => (string)s["name"]! == "llm"), "openinference.span.kind"));

        // CodeAct runs two cells per call: the input prefill and the model cell.
        var cells = spans.Where(s => (string)s["name"]! == "code_execution").ToList();
        Assert.Equal(4, cells.Count);
        Assert.All(cells, s => Assert.Equal("generation", Parent(s)));
        Assert.Equal(12, spans.Count);
    }

    [Fact]
    public async Task Quickstart06_SecretsInPromptsAndEnvironment_NeverReachTheTraceFile()
    {
        using var dir = new TempDirectory();
        using var env = new EnvVars();
        env.Set("QS06_SERVICE_API_KEY", "env-credential-value-1234567890");
        var example = new TracingExample(dir.Path);
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(1.0);" })
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"echo sk-abcdefghijklmnopqrstuv and env-credential-value-1234567890 and Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abcdef\");" });

        await Run(example, fake);

        var text = File.ReadAllText(example.TraceFile);
        Assert.DoesNotContain("eyJhbGci", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuv", text, StringComparison.Ordinal);
        Assert.DoesNotContain("env-credential-value-1234567890", text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quickstart07_DocCommentPlaceholder_IsFilledFromTheInstance()
    {
        var fake = new FakeLlmClient()
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Hola, como estas hoy?\");" })
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Hallo, wie geht es dir heute?\");" })
            .EnqueueToolCall("execute_csharp", new { code = "ReturnResult(\"Hola, como esta usted hoy?\");" });

        var output = await Run(new DynamicPromptsExample(), fake);

        Assert.Equal(
            "Original: Hello, how are you today?\nSpanish: Hola, como estas hoy?\nGerman: Hallo, wie geht es dir heute?\nSpanish (formal): Hola, como esta usted hoy?\n",
            output);
        Assert.Contains("Translate the text to Spanish.", All(fake.Calls[0]), StringComparison.Ordinal);
        Assert.Contains("Translate the text to German.", All(fake.Calls[1]), StringComparison.Ordinal);
        Assert.DoesNotContain("Translate the text to Spanish.", All(fake.Calls[1]), StringComparison.Ordinal);
        Assert.Contains("Translate the text to Spanish using formal register.", All(fake.Calls[2]), StringComparison.Ordinal);
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

        var output = await Run(new ContextBlocksExample(), fake);

        Assert.Equal("Answer: Automatic rollback if the error rate exceeds 1% for 2 minutes.\nAnswer: Roll back.\n", output);
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

    private static List<JsonNode> ReadSpans(string file) =>
        [.. File.ReadAllLines(file)
            .SelectMany(line => JsonNode.Parse(line)!["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]!.AsArray())
            .Select(s => s!)];

    private static string Attr(JsonNode span, string key) =>
        span["attributes"]!.AsArray().SingleOrDefault(a => (string)a!["key"]! == key)?["value"]?["stringValue"]?.ToString() ?? "";
}
