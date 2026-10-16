// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Strategies;

namespace Nooa.Tests.Generated;

/// <summary>
/// Minimal stand-in for the T12 <c>Agent</c> base class. It records each call and returns a scripted result.
/// </summary>
public abstract class RecordingAgent : IAgenticTarget
{
    public List<CurrentCall> Calls { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public object? Result { get; set; }

    public Task<T> InvokeAgenticAsync<T>(CurrentCall call, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        Tokens.Add(cancellationToken);
        return Task.FromResult((T)Result!);
    }

    public Task InvokeAgenticAsync(CurrentCall call, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}
