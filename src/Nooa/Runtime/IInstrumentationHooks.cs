// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/hooks.py

using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// Optional callbacks at the key points of a call, for tracing and logging. Every method does nothing by default,
/// so an implementation overrides only what it needs. The object that a <c>Before*</c> method returns goes to the
/// matching <c>After*</c> method. An exception in a hook never reaches the agent. Tracing (T19) implements this.
/// The code-execution hooks have their caller in CodeAct. Hooks for tool execution and message building are added with their callers.
/// </summary>
public interface IInstrumentationHooks
{
    /// <summary>Called when a traced agentic method starts.</summary>
    object? BeforeAgentCall(
        Agent agent, string methodName, IReadOnlyList<CallArgument> arguments, string callId, string? parentCallId) => null;

    /// <summary>Called when a traced agentic method ends, with its result or its exception.</summary>
    void AfterAgentCall(object? context, Agent agent, string methodName, object? result, Exception? exception)
    {
    }

    /// <summary>Called when a generation session starts. Strategies that are not traceable skip it.</summary>
    object? BeforeGeneration(
        Agent agent, string methodName, string strategy, string generationId, string? parentGenerationId, string? agentCallId) => null;

    /// <summary>Called when a generation session ends.</summary>
    void AfterGeneration(object? context, Agent agent, string methodName, object? result, Exception? exception, string generationId)
    {
    }

    /// <summary>Called before CodeAct runs a code cell, also the prefill cell.</summary>
    /// <param name="agent">The agent that runs the call.</param>
    /// <param name="methodName">The method that the model runs.</param>
    /// <param name="code">The code of the cell. It is model text.</param>
    /// <param name="toolCallId">The id of the tool call that carries the cell.</param>
    /// <param name="generationId">The id of the generation session, or null.</param>
    object? BeforeCodeExecution(Agent agent, string methodName, string code, string toolCallId, string? generationId) => null;

    /// <summary>Called after a cell ran, was refused, failed or was cancelled.</summary>
    /// <param name="context">The object that <see cref="BeforeCodeExecution"/> returned.</param>
    /// <param name="agent">The agent that runs the call.</param>
    /// <param name="methodName">The method that the model runs.</param>
    /// <param name="result">The outcome of the cell.</param>
    void AfterCodeExecution(object? context, Agent agent, string methodName, CellResult result)
    {
    }
}
