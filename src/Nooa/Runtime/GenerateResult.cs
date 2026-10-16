// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py

using Nooa.Llm;

namespace Nooa.Runtime;

/// <summary>The result of <see cref="IRuntimeServices.GenerateAsync"/>.</summary>
/// <param name="Response">The model response.</param>
/// <param name="EventTag">The tag of the recorded response event. It identifies the provider turn for linking.</param>
public sealed record GenerateResult(LlmResponse Response, string EventTag);
