// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test strategy: sends the first argument as a user message and returns the response text.</summary>
public class EchoStrategy : GenerationStrategy
{
    public List<string?> GenerationIds { get; } = [];

    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        GenerationIds.Add(runtime.GenerationId);
        var text = call.Arguments.Count > 0 ? Convert.ToString(call.Arguments[0].Value) ?? "" : "";
        var result = await runtime.GenerateAsync([LlmMessage.User(text)], cancellationToken: cancellationToken);
        return result.Response.Content;
    }
}
