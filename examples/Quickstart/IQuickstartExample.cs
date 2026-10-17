// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Examples.Quickstart;

/// <summary>One quickstart example. It runs against any model client, so a test can use a scripted fake.</summary>
public interface IQuickstartExample
{
    /// <summary>The example number, from 1 to 8.</summary>
    int Number { get; }

    /// <summary>A short title.</summary>
    string Title { get; }

    /// <summary>Runs the example and writes its result to <paramref name="output"/>.</summary>
    Task RunAsync(ILlmClient llm, TextWriter output);
}
