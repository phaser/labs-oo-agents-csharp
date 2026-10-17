// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/method_wrapper.py

using System.Reflection;
using Microsoft.Extensions.Logging;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Events;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// The wrapper around every agentic call. Upstream the metaclass wraps each method. Here the generated body of an
/// <see cref="AgenticAttribute"/> method calls <see cref="IAgenticTarget"/>, so this runs for those calls only.
/// </summary>
/// <remarks>
/// It checks the nesting depth, pushes the call id, sets the parent agent, emits <see cref="BeforeAgentCall"/> and
/// <see cref="AfterAgentCall"/>, calls the instrumentation hooks, runs the <see cref="AgentCallContext"/> middleware
/// and then dispatches to the runtime. A <see cref="NoTraceAttribute"/> method pushes the id of its parent, so its
/// children attach to the nearest traced ancestor, and fires no hooks. Argument validation has no equivalent: the
/// C# compiler checks the argument types and the generator builds the argument list.
/// </remarks>
internal sealed class AgentMethodInvoker(ActorRuntime runtime)
{
    public async Task<object?> InvokeAsync(CurrentCall call, CancellationToken cancellationToken)
    {
        var agent = runtime.Agent;
        var logger = agent.Logger;
        var method = AgentMethodLookup.Find(agent.GetType(), call);
        var traced = !IsNoTrace(agent, method);
        var parentCallId = AgentContext.CurrentCallId;
        var maxDepth = agent.Execution.MaxNestingDepth;
        var depth = AgentContext.CallStack.Count;
        if (depth >= maxDepth)
        {
            throw new NooaRuntimeException(
                $"Exceeded maximum nesting depth of {maxDepth} for {agent.GetType().Name}.{call.MethodName}. "
                + $"Current depth: {depth}. Increase ExecutionConfig.MaxNestingDepth to allow deeper nesting.");
        }

        call = call with { ParentId = parentCallId };
        AgentContext.PushCall(traced ? call.Id : parentCallId);
        var previousParent = AgentContext.ParentAgent;
        AgentContext.ParentAgent = agent;
        var isTopLevel = previousParent is null;
        Emit(new BeforeAgentCall
        {
            MethodName = call.MethodName,
            CallId = call.Id,
            ParentCallId = parentCallId,
            IsTopLevel = isTopLevel,
            NeedsGeneration = true,
        });

        object? hookContext = null;
        var hookRan = false;
        object? result = null;
        Exception? failure = null;
        try
        {
            if (agent.EventManager.HasMiddleware<AgentCallContext>())
            {
                var context = new AgentCallContext { Agent = agent, MethodName = call.MethodName, Arguments = call.Arguments };
                context = await agent.EventManager.RunMiddlewareAsync(context, async ctx =>
                {
                    RunBeforeHook(ctx.Arguments);
                    ctx.Result = await runtime.DispatchAsync(call with { Arguments = ctx.Arguments }, method, cancellationToken).ConfigureAwait(false);
                    return ctx;
                }).ConfigureAwait(false);
                result = context.HasResult
                    ? context.Result
                    : throw new NooaRuntimeException(
                        "Agent call middleware returned without setting the result. A short-circuiting middleware must set the result before it returns.");
            }
            else
            {
                RunBeforeHook(call.Arguments);
                result = await runtime.DispatchAsync(call, method, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            Emit(new AfterAgentCall
            {
                MethodName = call.MethodName,
                CallId = call.Id,
                ParentCallId = parentCallId,
                IsTopLevel = isTopLevel,
                NeedsGeneration = true,
                Success = failure is null,
                ExceptionType = failure?.GetType().Name,
            });
            AgentContext.ParentAgent = previousParent;
            AgentContext.PopCall();
            if (hookRan)
            {
                InstrumentationHooks.CallAfter(logger, h => h.AfterAgentCall(hookContext, agent, call.MethodName, result, failure));
            }
        }

        void RunBeforeHook(IReadOnlyList<CallArgument> arguments)
        {
            if (!traced || InstrumentationHooks.Current is null)
            {
                return;
            }

            hookRan = true;
            hookContext = InstrumentationHooks.CallBefore(
                logger, h => h.BeforeAgentCall(agent, call.MethodName, arguments, call.Id, parentCallId));
        }

        void Emit(EventBase e)
        {
            try
            {
                agent.EventManager.Add(e);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Agent call event emission failed");
            }
        }
    }

    private static bool IsNoTrace(Agent agent, MethodInfo? method) =>
        Attribute.IsDefined(agent.GetType(), typeof(NoTraceAttribute), inherit: true)
        || (method is not null && Attribute.IsDefined(method, typeof(NoTraceAttribute), inherit: true));
}
