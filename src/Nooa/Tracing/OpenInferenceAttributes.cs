// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_hooks_impl.py

namespace Nooa.Tracing;

/// <summary>
/// Span attribute names and values from the OpenInference semantic conventions, plus the NOOA-specific names.
/// </summary>
public static class OpenInferenceAttributes
{
    /// <summary>The span kind attribute.</summary>
    public const string SpanKind = "openinference.span.kind";

    /// <summary>Span kind of an agent method call.</summary>
    public const string KindAgent = "AGENT";

    /// <summary>Span kind of an orchestration step, such as one generation session.</summary>
    public const string KindChain = "CHAIN";

    /// <summary>Span kind of a model call.</summary>
    public const string KindLlm = "LLM";

    /// <summary>Span kind of a tool or a REPL cell.</summary>
    public const string KindTool = "TOOL";

    /// <summary>The input of the span, as text.</summary>
    public const string InputValue = "input.value";

    /// <summary>The MIME type of <see cref="InputValue"/>.</summary>
    public const string InputMimeType = "input.mime_type";

    /// <summary>The output of the span, as text.</summary>
    public const string OutputValue = "output.value";

    /// <summary>The MIME type of <see cref="OutputValue"/>.</summary>
    public const string OutputMimeType = "output.mime_type";

    /// <summary>MIME type for JSON values.</summary>
    public const string MimeJson = "application/json";

    /// <summary>MIME type for plain text values.</summary>
    public const string MimeText = "text/plain";

    /// <summary>The session that groups the spans of one run.</summary>
    public const string SessionId = "session.id";

    /// <summary>The model name of an LLM span.</summary>
    public const string LlmModelName = "llm.model_name";

    /// <summary>Prefix of the flattened input messages of an LLM span.</summary>
    public const string LlmInputMessages = "llm.input_messages";

    /// <summary>Prefix of the flattened output messages of an LLM span.</summary>
    public const string LlmOutputMessages = "llm.output_messages";

    /// <summary>Prompt token count of an LLM span.</summary>
    public const string TokenCountPrompt = "llm.token_count.prompt";

    /// <summary>Completion token count of an LLM span.</summary>
    public const string TokenCountCompletion = "llm.token_count.completion";

    /// <summary>Total token count of an LLM span.</summary>
    public const string TokenCountTotal = "llm.token_count.total";

    /// <summary>Cached prompt tokens read, of an LLM span.</summary>
    public const string TokenCountCacheRead = "llm.token_count.prompt_details.cache_read";

    /// <summary>Prompt tokens written to the cache, of an LLM span.</summary>
    public const string TokenCountCacheWrite = "llm.token_count.prompt_details.cache_write";

    /// <summary>Reasoning tokens of an LLM span.</summary>
    public const string TokenCountReasoning = "llm.token_count.completion_details.reasoning";

    /// <summary>The tool name of a TOOL span.</summary>
    public const string ToolName = "tool.name";

    /// <summary>The tool id of a TOOL span.</summary>
    public const string ToolId = "tool.id";

    /// <summary>The id of a tool call.</summary>
    public const string ToolCallId = "tool_call.id";

    /// <summary>The function name of a tool call.</summary>
    public const string ToolCallFunctionName = "tool_call.function.name";

    /// <summary>The JSON arguments of a tool call.</summary>
    public const string ToolCallFunctionArguments = "tool_call.function.arguments";

    /// <summary>Hint for the trace viewer: which plugin renders the span.</summary>
    public const string ViewerPlugin = "nooa.viewer.plugin";

    /// <summary>Number of values that the scrubber redacted in the span.</summary>
    public const string SecretsRedactedCount = "secrets.redacted_count";
}
