// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart (a copy of the example type for the unit tests; the example has its own in examples/Quickstart)

using System.ComponentModel;

namespace Nooa.Tests.Strategies.Acceptance;

public sealed record FeedbackAnalysis
{
    [Description("Overall tone of the feedback: positive, negative, neutral or mixed.")]
    public required string Sentiment { get; init; }

    [Description("Specific subjects mentioned by the customer.")]
    public required IReadOnlyList<string> Topics { get; init; }

    [Description("How quickly the feedback needs a response: low, medium or high.")]
    public required string Urgency { get; init; }

    [Description("One-sentence faithful summary.")]
    public required string Summary { get; init; }

    [Description("Confidence in the analysis from 0 to 1.")]
    public required double Confidence { get; init; }
}
