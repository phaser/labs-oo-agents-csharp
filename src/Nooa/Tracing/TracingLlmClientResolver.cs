// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Llm;

namespace Nooa.Tracing;

/// <summary>
/// An <see cref="ILlmClientResolver"/> decorator that wraps every client in a <see cref="TracingLlmClient"/>.
/// Set it as <c>Agent.DefaultLlmResolver</c> or as <c>AgentOptions.LlmResolver</c> to trace the model calls of alias-resolved clients.
/// </summary>
/// <param name="inner">The resolver to wrap.</param>
public sealed class TracingLlmClientResolver(ILlmClientResolver inner) : ILlmClientResolver
{
    /// <inheritdoc />
    public ILlmClient Resolve(string alias)
    {
        var client = inner.Resolve(alias);
        return client as TracingLlmClient ?? new TracingLlmClient(client);
    }
}
