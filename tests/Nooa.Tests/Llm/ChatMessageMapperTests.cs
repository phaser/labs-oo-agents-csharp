// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ChatMessageMapperTests
{
    [Fact]
    public void ToChatMessages_ContentPartList_JoinsTheTextParts()
    {
        var message = new JsonMessage(new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = "a" },
                new JsonObject { ["type"] = "text", ["text"] = "b" }),
        });

        var mapped = ChatMessageMapper.ToChatMessages([message]);

        Assert.Equal("ab", Assert.Single(mapped).Text);
    }

    [Fact]
    public void ToChatMessages_ImagePart_Throws()
    {
        var message = new JsonMessage(new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "image_url" }),
        });

        Assert.Throws<ArgumentException>(() => ChatMessageMapper.ToChatMessages([message]));
    }

    [Fact]
    public void ToChatMessages_UnknownRole_Throws()
    {
        var message = new JsonMessage(new JsonObject { ["role"] = "narrator", ["content"] = "x" });

        Assert.Throws<ArgumentException>(() => ChatMessageMapper.ToChatMessages([message]));
    }

    [Fact]
    public void ToChatMessages_MissingRole_Throws()
    {
        Assert.Throws<ArgumentException>(() => ChatMessageMapper.ToChatMessages([new JsonMessage(new JsonObject())]));
    }

    [Fact]
    public void ToParts_MalformedToolArguments_AreKeptAsTextWhenRawIsAbsent()
    {
        var call = new FunctionCallContent("c1", "t", null) { Exception = new InvalidOperationException("bad json") };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]));

        var parts = ChatMessageMapper.ToParts(response);

        Assert.Equal("", Assert.IsType<ToolCall>(Assert.Single(parts)).Arguments);
    }

    [Fact]
    public void ToUsage_Null_ReturnsNull()
    {
        Assert.Null(ChatMessageMapper.ToUsage(null));
    }

    [Fact]
    public void ToFinishReason_ToolCallsReasonWithoutParts_IsToolCalls()
    {
        Assert.Equal(FinishReason.ToolCalls, ChatMessageMapper.ToFinishReason(ChatFinishReason.ToolCalls, hasToolCalls: false));
    }
}
