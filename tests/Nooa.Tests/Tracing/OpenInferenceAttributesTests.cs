// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_openinference_conformance.py

using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

public class OpenInferenceAttributesTests
{
    [Fact]
    public void Names_MatchTheOpenInferenceSpecification()
    {
        Assert.Equal("openinference.span.kind", OpenInferenceAttributes.SpanKind);
        Assert.Equal("input.value", OpenInferenceAttributes.InputValue);
        Assert.Equal("output.mime_type", OpenInferenceAttributes.OutputMimeType);
        Assert.Equal("session.id", OpenInferenceAttributes.SessionId);
        Assert.Equal("llm.token_count.prompt", OpenInferenceAttributes.TokenCountPrompt);
        Assert.Equal("llm.token_count.completion_details.reasoning", OpenInferenceAttributes.TokenCountReasoning);
        Assert.Equal("tool_call.function.arguments", OpenInferenceAttributes.ToolCallFunctionArguments);
    }

    [Fact]
    public void KindValues_AreTheUpperCaseSpecValues()
    {
        Assert.Equal(["AGENT", "CHAIN", "LLM", "TOOL"], [OpenInferenceAttributes.KindAgent, OpenInferenceAttributes.KindChain, OpenInferenceAttributes.KindLlm, OpenInferenceAttributes.KindTool]);
    }
}
