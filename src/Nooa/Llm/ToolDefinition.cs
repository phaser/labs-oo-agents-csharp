// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.Json.Nodes;

namespace Nooa.Llm;

/// <summary>
/// The model-facing description of one tool: name, description and JSON schema of the parameters.
/// The callable stays with the runtime; the LLM client never executes a tool.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">What the tool does. The model reads it.</param>
/// <param name="ParametersSchema">The JSON schema of the parameters object.</param>
public sealed record ToolDefinition(string Name, string Description, JsonObject ParametersSchema);
