// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/fake.py

using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>Raised when strict <see cref="FakeLlmClient"/> execution has no scripted response left.</summary>
public sealed class FakeLlmResponseExhaustedException : NooaException
{
    public FakeLlmResponseExhaustedException(string message)
        : base(message)
    {
    }
}
