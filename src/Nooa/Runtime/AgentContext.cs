// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context_vars.py

using System.Text.Json.Nodes;
using Nooa.Config;
using Nooa.Llm;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// Ambient state of the running agent call, held in <see cref="AsyncLocal{T}"/> (decision D9). Parallel tasks
/// that start inside a call inherit the values, and a change in one task does not reach its siblings.
/// The stacks are arrays that are copied on every change.
/// </summary>
internal static class AgentContext
{
    private static readonly AsyncLocal<Agent?> _parentAgent = new();
    private static readonly AsyncLocal<string?[]?> _callStack = new();
    private static readonly AsyncLocal<string[]?> _generationIds = new();
    private static readonly AsyncLocal<bool> _inGenerationSession = new();
    private static readonly AsyncLocal<bool> _evaluatingExpression = new();
    private static readonly AsyncLocal<CurrentCall?> _currentCall = new();
    private static readonly AsyncLocal<ILlmClient?> _currentLlm = new();
    private static readonly AsyncLocal<GenerationStrategy?> _currentStrategy = new();
    private static readonly AsyncLocal<TruncationConfig?> _currentTruncation = new();
    private static readonly AsyncLocal<JsonNode?> _currentEventFormat = new();
    private static readonly AsyncLocal<(Agent Agent, ILlmClient Client)?> _llmOverride = new();
    private static readonly AsyncLocal<(Agent Agent, GenerationStrategy Strategy)?> _strategyOverride = new();

    /// <summary>The agent that runs the current call. Child agents inherit its LLM.</summary>
    public static Agent? ParentAgent { get => _parentAgent.Value; set => _parentAgent.Value = value; }

    /// <summary>True inside a generation session. A nested call then runs inline and does not take the lock.</summary>
    public static bool InGenerationSession { get => _inGenerationSession.Value; set => _inGenerationSession.Value = value; }

    /// <summary>True while a context expression runs. An agentic call that starts in that scope fails.</summary>
    public static bool EvaluatingExpression { get => _evaluatingExpression.Value; set => _evaluatingExpression.Value = value; }

    /// <summary>The call that the strategy generates.</summary>
    public static CurrentCall? CurrentCall { get => _currentCall.Value; set => _currentCall.Value = value; }

    /// <summary>The effective LLM client of the current call.</summary>
    public static ILlmClient? CurrentLlm { get => _currentLlm.Value; set => _currentLlm.Value = value; }

    /// <summary>The strategy of the current call.</summary>
    public static GenerationStrategy? CurrentStrategy { get => _currentStrategy.Value; set => _currentStrategy.Value = value; }

    /// <summary>The truncation config of the current call.</summary>
    public static TruncationConfig? CurrentTruncation { get => _currentTruncation.Value; set => _currentTruncation.Value = value; }

    /// <summary>The event format that the event manager copies into the metadata of new events.</summary>
    public static JsonNode? CurrentEventFormat { get => _currentEventFormat.Value; set => _currentEventFormat.Value = value; }

    /// <summary>A call-level LLM override from <see cref="Agent.UseLlm(ILlmClient)"/>.</summary>
    public static (Agent Agent, ILlmClient Client)? LlmOverride { get => _llmOverride.Value; set => _llmOverride.Value = value; }

    /// <summary>A call-level strategy override from <see cref="Agent.UseStrategy"/>.</summary>
    public static (Agent Agent, GenerationStrategy Strategy)? StrategyOverride { get => _strategyOverride.Value; set => _strategyOverride.Value = value; }

    /// <summary>The call ids from the outermost to the innermost call. An entry is null for an untraced top-level call.</summary>
    public static IReadOnlyList<string?> CallStack => _callStack.Value ?? [];

    /// <summary>The id of the innermost call, or null.</summary>
    public static string? CurrentCallId => _callStack.Value is { Length: > 0 } stack ? stack[^1] : null;

    /// <summary>The generation ids from the outermost to the innermost session.</summary>
    public static IReadOnlyList<string> GenerationIds => _generationIds.Value ?? [];

    /// <summary>Pushes a call id. The stack is copied so that sibling tasks keep their own stack.</summary>
    public static void PushCall(string? callId) => _callStack.Value = [.. _callStack.Value ?? [], callId];

    /// <summary>Pops the innermost call id.</summary>
    public static void PopCall()
    {
        if (_callStack.Value is { Length: > 0 } stack)
        {
            _callStack.Value = stack[..^1];
        }
    }

    /// <summary>Pushes a generation id.</summary>
    public static void PushGeneration(string id) => _generationIds.Value = [.. _generationIds.Value ?? [], id];

    /// <summary>Pops the innermost generation id.</summary>
    public static void PopGeneration()
    {
        if (_generationIds.Value is { Length: > 0 } ids)
        {
            _generationIds.Value = ids[..^1];
        }
    }
}
