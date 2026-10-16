// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_empty_content_retry.py

using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class EmptyContentExceptionTests
{
    [Fact]
    public void Constructor_WithReasoning_KeepsAndShortensItInTheMessage()
    {
        var reasoning = new string('x', 150);

        var error = new EmptyContentException(reasoning);

        Assert.Equal(reasoning, error.Reasoning);
        Assert.Equal($"Empty content with reasoning: {new string('x', 100)}...", error.Message);
        Assert.IsAssignableFrom<NooaException>(error);
    }

    [Fact]
    public void Constructor_WithoutReasoning_HasPlainMessage()
    {
        var error = new EmptyContentException();

        Assert.Null(error.Reasoning);
        Assert.Equal("Empty content", error.Message);
    }
}
