// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_hooks_impl.py

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Nooa.Runtime;
using Nooa.Strategies;

namespace Nooa.Tracing;

/// <summary>
/// The tracing implementation of <see cref="IInstrumentationHooks"/>: an AGENT span for each traced agent call and a CHAIN span
/// for each generation session, with OpenInference attribute names. The spans come from <see cref="NooaTracing.Source"/>.
/// </summary>
/// <remarks>
/// <para>
/// A method or class with <see cref="NoTraceAttribute"/> gets no AGENT span (the runtime skips the hook). This class also gives it
/// no generation span, and no LLM span inside its generation, because they would carry its arguments and result.
/// Its traced children attach to the nearest traced ancestor.
/// </para>
/// <para>
/// Deviation: the code-execution, tool-execution and message-built hooks of upstream are not ported here.
/// The hook interface has no such methods yet; T17 adds them with their callers.
/// </para>
/// </remarks>
public sealed class OpenInferenceHooks : IInstrumentationHooks
{
    private readonly ConcurrentDictionary<string, Activity> _active = new();

    /// <inheritdoc />
    public object? BeforeAgentCall(
        Agent agent, string methodName, IReadOnlyList<CallArgument> arguments, string callId, string? parentCallId)
    {
        var span = Start($"method.{methodName}", parentCallId);
        if (span is null)
        {
            return null;
        }

        Stamp(span, OpenInferenceAttributes.KindAgent, "method");
        SpanAttributes.Set(span, "agent.name", agent.GetType().Name);
        SpanAttributes.Set(span, "agent.method", methodName);
        SpanAttributes.Set(span, "agent.call_id", callId);
        SpanAttributes.Set(span, "agent.parent_call_id", parentCallId);
        SpanAttributes.Set(
            span,
            "agent.method_signature",
            $"{methodName}({string.Join(", ", arguments.Select(a => $"{a.Type.Name} {a.Name}"))})");
        span.AddEvent(new ActivityEvent("status_update", tags: new ActivityTagsCollection { ["message"] = $"Running agent method: {agent.GetType().Name}.{methodName}" }));

        var input = SpanAttributes.SafeJson(
        [
            new("args", Array.Empty<object?>()),
            new("kwargs", arguments.ToDictionary(a => a.Name, a => a.Value)),
        ]);
        SpanAttributes.Set(span, OpenInferenceAttributes.InputValue, input);
        SpanAttributes.Set(span, OpenInferenceAttributes.InputMimeType, OpenInferenceAttributes.MimeJson);

        _active[callId] = span;
        return new SpanScope(span, callId, PreviousSuppression: false);
    }

    /// <inheritdoc />
    public void AfterAgentCall(object? context, Agent agent, string methodName, object? result, Exception? exception)
    {
        if (context is not SpanScope scope)
        {
            return;
        }

        Complete(scope, result, exception, includeResultType: false);
    }

    /// <inheritdoc />
    public object? BeforeGeneration(
        Agent agent, string methodName, string strategy, string generationId, string? parentGenerationId, string? agentCallId)
    {
        var previous = TraceSuppression.IsActive;
        var noTrace = IsNoTrace(agent, methodName);
        TraceSuppression.IsActive = noTrace;
        if (noTrace)
        {
            return new SpanScope(null, generationId, previous);
        }

        // Prefer the AGENT span of this call, so that the generation of a sub-agent hangs under the sub-agent.
        var span = Start("generation", agentCallId, parentGenerationId);
        if (span is null)
        {
            return new SpanScope(null, generationId, previous);
        }

        Stamp(span, OpenInferenceAttributes.KindChain, "generation");
        SpanAttributes.Set(span, "agent.name", agent.GetType().Name);
        SpanAttributes.Set(span, "agent.method", methodName);
        SpanAttributes.Set(span, "generation.strategy", strategy);
        SpanAttributes.Set(span, "generation.id", generationId);
        SpanAttributes.Set(span, "generation.parent_id", parentGenerationId);
        SpanAttributes.Set(span, "agent.call_id", agentCallId);

        _active[generationId] = span;
        return new SpanScope(span, generationId, previous);
    }

    /// <inheritdoc />
    public void AfterGeneration(object? context, Agent agent, string methodName, object? result, Exception? exception, string generationId)
    {
        if (context is not SpanScope scope)
        {
            return;
        }

        TraceSuppression.IsActive = scope.PreviousSuppression;
        Complete(scope, result, exception, includeResultType: true);
    }

    /// <summary>
    /// Ends every span that is still open, with an error status, so that an exporter sees them.
    /// Call it before <see cref="NooaTracing.Shutdown"/> when a task is cancelled or times out.
    /// </summary>
    /// <returns>The number of spans that this call ended.</returns>
    public int EndActiveSpans(string reason = "timeout")
    {
        var count = 0;
        foreach (var key in _active.Keys.Reverse())
        {
            if (_active.TryRemove(key, out var span))
            {
                span.SetStatus(ActivityStatusCode.Error, reason);
                SpanAttributes.Set(span, "error.type", reason.Contains("timeout", StringComparison.OrdinalIgnoreCase) ? "Timeout" : "Cancelled");
                SpanAttributes.Set(span, "error.message", reason);
                span.Stop();
                count++;
            }
        }

        return count;
    }

    private static void Stamp(Activity span, string kind, string viewerPlugin)
    {
        SpanAttributes.Set(span, OpenInferenceAttributes.SpanKind, kind);
        SpanAttributes.Set(span, OpenInferenceAttributes.ViewerPlugin, viewerPlugin);
        SpanAttributes.Set(span, OpenInferenceAttributes.SessionId, TraceSession.Current);
    }

    private void Complete(SpanScope scope, object? result, Exception? exception, bool includeResultType)
    {
        if (scope.Span is not { } span)
        {
            return;
        }

        _active.TryRemove(scope.Key, out _);
        if (exception is not null)
        {
            SpanAttributes.RecordError(span, exception);
        }
        else
        {
            span.SetStatus(ActivityStatusCode.Ok);
            SpanAttributes.Set(span, OpenInferenceAttributes.OutputValue, SpanAttributes.SafeSerialize(result));
            SpanAttributes.Set(span, OpenInferenceAttributes.OutputMimeType, OpenInferenceAttributes.MimeText);
            if (includeResultType)
            {
                SpanAttributes.Set(span, "result.type", result?.GetType().Name ?? "None");
            }
        }

        span.Stop();
    }

    // The parent is the first id that has an open span, else the current activity. Setting Current first makes the new span
    // a child by object, so that Stop() restores the parent as the current activity.
    private Activity? Start(string name, params string?[] parentIds)
    {
        foreach (var id in parentIds)
        {
            if (id is not null && _active.TryGetValue(id, out var parent))
            {
                Activity.Current = parent;
                break;
            }
        }

        return NooaTracing.Source.StartActivity(name, ActivityKind.Internal);
    }

    private static bool IsNoTrace(Agent agent, string methodName)
    {
        var type = agent.GetType();
        if (Attribute.IsDefined(type, typeof(NoTraceAttribute), inherit: true))
        {
            return true;
        }

        // Match by name only: when an overload has the attribute, fail closed.
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetMethods(all).Any(m => m.Name == methodName && Attribute.IsDefined(m, typeof(NoTraceAttribute), inherit: true));
    }

    private sealed record SpanScope(Activity? Span, string Key, bool PreviousSuppression);
}
