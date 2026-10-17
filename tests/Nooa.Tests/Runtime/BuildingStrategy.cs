// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.ContextBlocks;
using Nooa.Events;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tests.Runtime;

/// <summary>Test strategy: records a task event from the first argument, builds the messages and sends them.</summary>
public sealed class BuildingStrategy : GenerationStrategy
{
    public IReadOnlyList<LlmMessage> Messages { get; private set; } = [];

    public ContextWindowStats? StatsBeforeCall { get; private set; }

    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        var text = call.Arguments.Count > 0 ? Convert.ToString(call.Arguments[0].Value) ?? "" : "";
        runtime.EventManager.Add(new TaskEvent { Prompt = text });
        Messages = runtime.BuildMessages();
        StatsBeforeCall = runtime.LastContextStats;
        var result = await runtime.GenerateAsync(Messages, cancellationToken: cancellationToken);
        return result.Response.Content;
    }
}
