// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/llm_types.py

using System.Text.Json.Serialization;
using Nooa.Json;

namespace Nooa.Llm;

/// <summary>One ordered part of an assistant turn. Parts are immutable. Persisted with a <c>kind</c> discriminator.</summary>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AssistantText), "text")]
[JsonDerivedType(typeof(ToolCall), "tool_call")]
[JsonDerivedType(typeof(AssistantReasoning), "reasoning")]
public abstract record AssistantPart
{
    /// <summary>Immutable adapter-only JSON extensions. Not shown to the model.</summary>
    public NativeJson? Native { get; init; }
}
