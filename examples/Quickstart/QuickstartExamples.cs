// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Examples.Quickstart.Example01;
using Nooa.Examples.Quickstart.Example02;
using Nooa.Examples.Quickstart.Example03;
using Nooa.Examples.Quickstart.Example04;
using Nooa.Examples.Quickstart.Example05;
using Nooa.Examples.Quickstart.Example06;
using Nooa.Examples.Quickstart.Example07;
using Nooa.Examples.Quickstart.Example08;

namespace Nooa.Examples.Quickstart;

/// <summary>The eight quickstart examples, in order.</summary>
public static class QuickstartExamples
{
    /// <summary>Creates the examples with their default settings.</summary>
    public static IReadOnlyList<IQuickstartExample> CreateAll() =>
    [
        new FirstGenerationMethodExample(),
        new StructuredOutputExample(),
        new CodeActToolsExample(),
        new StrategiesExample(),
        new ProgressiveDisclosureExample(),
        new TracingExample(),
        new DynamicPromptsExample(),
        new ContextBlocksExample(),
    ];
}
