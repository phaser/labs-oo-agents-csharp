// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/__init__.py

namespace Nooa.Errors;

/// <summary>
/// Generation was explicitly aborted by the agent.
/// </summary>
public class GenerationAbortedException : GenerationException
{
    public GenerationAbortedException(string message)
        : base(message)
    {
    }
}
