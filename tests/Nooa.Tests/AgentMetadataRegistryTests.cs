// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

public class AgentMetadataRegistryTests
{
    private sealed class Registered;

    private sealed class Missing;

    [Fact]
    public void Register_ThenTryGet_ReturnsTheLatestMetadata()
    {
        AgentMetadataRegistry.Register(new AgentMetadata(typeof(Registered), "a", null));
        AgentMetadataRegistry.Register(new AgentMetadata(typeof(Registered), "b", static _ => "prompt"));

        Assert.True(AgentMetadataRegistry.TryGet(typeof(Registered), out var metadata));
        Assert.Equal("b", metadata!.Llm);
        Assert.Equal("prompt", metadata.SystemPrompt!(new object()));
    }

    [Fact]
    public void TryGet_UnknownType_ReturnsFalse()
    {
        Assert.False(AgentMetadataRegistry.TryGet(typeof(Missing), out var metadata));
        Assert.Null(metadata);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AgentMetadataRegistry.Register(null!));
        Assert.Throws<ArgumentNullException>(() => AgentMetadataRegistry.TryGet(null!, out _));
    }
}
