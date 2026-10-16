// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/__init__.py

namespace Nooa.Errors;

/// <summary>
/// Generation stopped because the model repeated a tool call after a loop-guard warning.
/// </summary>
public class LoopDetectedException : GenerationException
{
    public LoopDetectedException(string message)
        : base(message)
    {
    }
}
