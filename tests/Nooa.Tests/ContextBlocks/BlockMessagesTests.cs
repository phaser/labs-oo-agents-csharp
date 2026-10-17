// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/context_blocks/test_formatters.py

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Llm;

namespace Nooa.Tests.ContextBlocks;

public class BlockMessagesTests
{
    private static ResolvedBlock Block(EventBase evt, string content = "", string tag = "1") =>
        new("event_" + tag, content, evt.EventRole, new BlockMetadata(Tag: tag), evt);

    private static LlmResponse Response(params string[] callIds) => new LlmResponse
    {
        Parts = [new AssistantText { Text = "calling" }, .. callIds.Select(c => (AssistantPart)new ToolCall { Id = c, Name = "lookup", Arguments = "{\"q\":1}" })],
        FinishReason = callIds.Length > 0 ? FinishReason.ToolCalls : FinishReason.Stop,
    };

    private static ToolCallEvent Execution(string callId, string? responseId, string? result = "result") => new()
    {
        ToolCallId = callId,
        Name = "lookup",
        Arguments = new JsonObject { ["q"] = 1 },
        LlmResponseId = responseId,
        Result = result is null ? null : new ToolResult { ToolCallId = callId, Content = result },
    };

    private static List<LlmMessage> Run(params ResolvedBlock[] blocks) =>
        BlockMessages.FromEventBlocks(blocks, BlockWrapper.XmlMessageContent, NullLogger.Instance);

    private static string Role(LlmMessage message) => ((JsonMessage)message).Value["role"]!.GetValue<string>();

    private static string Content(LlmMessage message) => ((JsonMessage)message).Value["content"]!.GetValue<string>();

    [Fact]
    public void FromEventBlocks_LinkedExecutions_GroupUnderTheirAssistantTurn()
    {
        var response = Response("c1", "c2");
        var messages = Run(
            Block(response, tag: "1"),
            Block(Execution("c2", response.Id, "two"), tag: "3"),
            Block(Execution("c1", response.Id, "one"), tag: "2"));

        Assert.Equal(3, messages.Count);
        Assert.Same(response, Assert.IsType<ResponseMessage>(messages[0]).Response);
        Assert.Equal(("tool", "one"), (Role(messages[1]), Content(messages[1])));
        Assert.Equal(("tool", "two"), (Role(messages[2]), Content(messages[2])));
        Assert.Equal("c1", ((JsonMessage)messages[1]).Value["tool_call_id"]!.GetValue<string>());
    }

    [Fact]
    public void FromEventBlocks_IncompleteBatch_OmitsTurnAndExecutions()
    {
        var response = Response("c1", "c2");
        var messages = Run(Block(response), Block(Execution("c1", response.Id), tag: "2"));

        Assert.Empty(messages);
    }

    [Fact]
    public void FromEventBlocks_ExecutionWithoutResult_OmitsTheBatch()
    {
        var response = Response("c1");

        Assert.Empty(Run(Block(response), Block(Execution("c1", response.Id, result: null), tag: "2")));
    }

    [Fact]
    public void FromEventBlocks_DuplicateCallIds_OmitsTheBatch()
    {
        var response = Response("c1", "c1");

        Assert.Empty(Run(Block(response), Block(Execution("c1", response.Id), tag: "2")));
    }

    [Fact]
    public void FromEventBlocks_LinkedExecutionWithoutItsResponse_IsNotRendered() =>
        Assert.Empty(Run(Block(Execution("c1", "missing-response"))));

    [Fact]
    public void FromEventBlocks_MalformedToolCalls_ReplayAsATextOnlyTurn()
    {
        var response = new LlmResponse
        {
            Parts = [new AssistantText { Text = "oops" }, new ToolCall { Id = "c1", Name = "t", Arguments = "{broken" }],
            FinishReason = FinishReason.ToolCalls,
        };

        var message = Assert.IsType<JsonMessage>(Assert.Single(Run(Block(response))));

        Assert.Equal("assistant", message.Value["role"]!.GetValue<string>());
        Assert.Equal("oops", message.Value["content"]!.GetValue<string>());
        Assert.Null(message.Value["tool_calls"]);
    }

    [Fact]
    public void FromEventBlocks_PlainResponse_ReplaysTheResponse()
    {
        var response = Response();

        Assert.Same(response, Assert.IsType<ResponseMessage>(Assert.Single(Run(Block(response)))).Response);
    }

    [Fact]
    public void FromEventBlocks_EmptyResponse_IsSkipped() => Assert.Empty(Run(Block(new LlmResponse())));

    [Fact]
    public void FromEventBlocks_StandaloneToolCall_BecomesCallAndResult()
    {
        var messages = Run(Block(Execution("c1", responseId: null, "done")));

        var call = Assert.IsType<JsonMessage>(messages[0]).Value;
        Assert.Equal("assistant", call["role"]!.GetValue<string>());
        var function = call["tool_calls"]![0]!["function"]!;
        Assert.Equal("lookup", function["name"]!.GetValue<string>());
        Assert.Equal("{\"q\":1}", function["arguments"]!.GetValue<string>());
        Assert.Equal("done", Content(messages[1]));
    }

    [Fact]
    public void FromEventBlocks_StandaloneToolCallWithoutResult_GetsAPlaceholder()
    {
        var messages = Run(Block(Execution("c1", responseId: null, result: null)));

        Assert.Equal("(no result recorded)", Content(messages[1]));
    }

    [Fact]
    public void FromEventBlocks_UserEvent_IsWrappedWithItsTag()
    {
        var message = Assert.Single(Run(Block(new TaskEvent { Prompt = "p" }, "Task(...)", "4")));

        Assert.Equal(("user", "<sys tag=\"4\">\nTask(...)\n</sys>"), (Role(message), Content(message)));
    }

    [Fact]
    public void FromEventBlocks_AssistantEvent_ReplaysVerbatim()
    {
        var message = Assert.Single(Run(Block(new Message { Content = "hello" }, "hello")));

        Assert.Equal(("assistant", "hello"), (Role(message), Content(message)));
    }

    [Fact]
    public void FromEventBlocks_RuntimeAndMetadataEvents_AreNeverSent()
    {
        Assert.Empty(Run(Block(new BeforeTurn { MethodName = "m", Strategy = "s", GenerationId = "g", TurnNumber = 1 }), Block(new TextOnlyReply { Content = "x" }, "x", "2")));
    }

    [Fact]
    public void FromEventBlocks_EventWithImages_BecomesAContentArray()
    {
        var image = new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = "data:x" } };
        var task = new TaskEvent { Prompt = "look", Images = [image] };

        var message = Assert.IsType<JsonMessage>(Assert.Single(Run(Block(task, "Task(look)"))));

        var parts = message.Value["content"]!.AsArray();
        Assert.Equal(2, parts.Count);
        Assert.Equal("text", parts[0]!["type"]!.GetValue<string>());
        Assert.Equal("image_url", parts[1]!["type"]!.GetValue<string>());
        Assert.NotSame(image, parts[1]);
    }

    [Fact]
    public void FromEventBlocks_WithoutWrap_UsesTheRawContent()
    {
        var message = Assert.Single(BlockMessages.FromEventBlocks([Block(new TaskEvent { Prompt = "p" }, "raw")], null, NullLogger.Instance));

        Assert.Equal("raw", Content(message));
    }

    [Fact]
    public void Build_NoBlocks_GivesNoMessages() =>
        Assert.Empty(BlockMessages.Build([], BlockWrapper.XmlSystemBlock, BlockWrapper.XmlMessageContent, NullLogger.Instance));

    [Fact]
    public void Build_OnlyEvents_StartsWithAnEmptySystemMessage()
    {
        var messages = BlockMessages.Build(
            [Block(new TaskEvent { Prompt = "p" }, "t")], BlockWrapper.XmlSystemBlock, BlockWrapper.XmlMessageContent, NullLogger.Instance);

        Assert.Equal(2, messages.Count);
        Assert.Equal(("system", ""), (Role(messages[0]), Content(messages[0])));
    }
}
