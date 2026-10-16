// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

namespace Nooa.Llm;

/// <summary>
/// The NOOA-owned LLM client surface that the runtime calls (the abstract surface of upstream <c>UnifiedLLM</c>).
/// Provider transports, such as the <c>IChatClient</c> adapter, implement it.
/// </summary>
/// <remarks>
/// Per-call keyword overrides (<c>**kwargs</c> upstream) are not part of the interface. Call settings
/// belong to the client configuration (T09, T10).
/// </remarks>
public interface ILlmClient
{
    /// <summary>The model context window in tokens, or null when it is unknown.</summary>
    int? ContextWindow { get; }

    /// <summary>Counts the tokens in <paramref name="text"/>. Implementations may estimate.</summary>
    int CountTokens(string text);

    /// <summary>
    /// Sends one request and returns the normalized response.
    /// </summary>
    /// <param name="messages">The ordered input: chat messages, earlier responses and cache boundaries.</param>
    /// <param name="tools">The tools that the model may call, or null for none.</param>
    /// <param name="outputType">The type of the structured output to request, or null for free text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task<LlmResponse> CallAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        Type? outputType = null,
        CancellationToken cancellationToken = default);
}
