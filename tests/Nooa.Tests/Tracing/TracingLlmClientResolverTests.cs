// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Llm;
using Nooa.Tests.Runtime;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

public class TracingLlmClientResolverTests
{
    [Fact]
    public void Resolve_WrapsTheClientInATracingClient()
    {
        var stub = new StubLlmResolver();
        stub.Clients["m"] = new FakeLlmClient();

        var client = new TracingLlmClientResolver(stub).Resolve("m");

        Assert.IsType<TracingLlmClient>(client);
        Assert.Equal(["m"], stub.Requests);
    }

    [Fact]
    public void Resolve_AlreadyTracedClient_IsNotWrappedTwice()
    {
        var traced = new TracingLlmClient(new FakeLlmClient());
        var inner = new FixedResolver(traced);

        Assert.Same(traced, new TracingLlmClientResolver(inner).Resolve("any"));
    }

    [Fact]
    public void Resolve_UnknownAlias_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => new TracingLlmClientResolver(new StubLlmResolver()).Resolve("missing"));

    private sealed class FixedResolver(ILlmClient client) : ILlmClientResolver
    {
        public ILlmClient Resolve(string alias) => client;
    }
}
