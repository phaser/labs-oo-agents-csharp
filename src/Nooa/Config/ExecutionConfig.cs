// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/execution_config.py

namespace Nooa.Config;

/// <summary>
/// Framework-level execution guards, set at agent class level.
/// </summary>
public sealed record ExecutionConfig : ConfigBase
{
    /// <summary>Maximum agent-in-agent recursion depth.</summary>
    public int MaxNestingDepth { get; init { field = value; MarkSet(); } } = 10;

    /// <summary>Returns a copy of this config with every field that <paramref name="other"/> sets explicitly.</summary>
    public ExecutionConfig MergeWith(ExecutionConfig other)
    {
        RequireFieldsSet(other);
        return CopySetFields(this with { }, other);
    }
}
