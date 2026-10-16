// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.AI;

namespace Nooa.Tests.Llm;

/// <summary>A fake chat client: each call takes the next scripted step, which is a response or an exception.</summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<CancellationToken, Task<ChatResponse>>> _steps = new();

    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public ScriptedChatClient Reply(ChatResponse response) => Then(_ => Task.FromResult(response));

    public ScriptedChatClient Reply(string text) => Reply(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    public ScriptedChatClient Fail(Exception error) => Then(_ => Task.FromException<ChatResponse>(error));

    public ScriptedChatClient Then(Func<CancellationToken, Task<ChatResponse>> step)
    {
        _steps.Enqueue(step);
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((messages.ToList(), options));
        return _steps.Dequeue()(cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
