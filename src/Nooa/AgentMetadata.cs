// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Class-level data that the source generator captures for one agent type.
/// </summary>
/// <param name="AgentType">The agent class.</param>
/// <param name="Llm">The <see cref="AgentOptionsAttribute.Llm"/> value, or null.</param>
/// <param name="SystemPrompt">
/// Renders the class XML doc comment (the system prompt) for an agent instance. Null when the class has no doc comment.
/// </param>
public sealed record AgentMetadata(Type AgentType, string? Llm, Func<object, string>? SystemPrompt);
