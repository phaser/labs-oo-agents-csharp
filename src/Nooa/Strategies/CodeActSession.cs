// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (CodeActSession)

using Nooa.Errors;

namespace Nooa.Strategies;

/// <summary>
/// The counters of one CodeAct generation session. The session ends when the iterations or the retries run out.
/// The REPL state (variables, <c>Out[n]</c>) lives in the execution engine, not here.
/// </summary>
internal sealed class CodeActSession(int? maxIterations, int maxRetries, string methodName)
{
    /// <summary>The iteration limit. Null means no limit.</summary>
    public int? MaxIterations { get; } = maxIterations;

    /// <summary>The number of errors that ends the session.</summary>
    public int MaxRetries { get; } = maxRetries;

    /// <summary>The name of the method that the model runs.</summary>
    public string MethodName { get; } = methodName;

    /// <summary>Completed model turns that did work.</summary>
    public int Iteration { get; private set; }

    /// <summary>Errors so far.</summary>
    public int ErrorCount { get; private set; }

    /// <summary>Consecutive turns with a text reply and no tool call.</summary>
    public int ConsecutiveTextOnly { get; private set; }

    /// <summary>True when the loop must stop: out of iterations or out of retries.</summary>
    public bool IsExhausted => (MaxIterations is { } max && Iteration >= max) || ErrorCount >= MaxRetries;

    public void RecordIteration() => Iteration++;

    public void RecordError() => ErrorCount++;

    public void RecordTextOnly() => ConsecutiveTextOnly++;

    public void ResetTextOnly() => ConsecutiveTextOnly = 0;

    /// <summary>The error that ends the session when the loop is exhausted.</summary>
    public GenerationException BuildFailure() => new(CodeActPrompts.FailureMessage(this));
}
