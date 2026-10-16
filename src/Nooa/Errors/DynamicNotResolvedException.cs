// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/exceptions.py

namespace Nooa.Errors;

/// <summary>
/// A caller read a dynamic context block before the first model turn resolved it.
/// Dynamic blocks are evaluated at the start of each model turn.
/// </summary>
public class DynamicNotResolvedException : NooaException
{
    /// <summary>Creates the exception for a block key.</summary>
    public DynamicNotResolvedException(string key)
        : base($"Dynamic context block \"{key}\" has not been resolved yet. Dynamic blocks are evaluated at the start of each model turn.")
    {
        Key = key;
    }

    /// <summary>The key of the unresolved block.</summary>
    public string Key { get; }
}
