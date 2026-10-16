// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/exceptions.py

namespace Nooa.Errors;

/// <summary>
/// A caller tried to change or remove a protected context block. The framework sets protected blocks
/// (<c>system_prompt</c>, <c>self</c>, <c>state</c>). Model-written code cannot change them.
/// </summary>
public class ProtectedBlockException : NooaException
{
    /// <summary>Creates the exception for a block key and the operation that was refused.</summary>
    public ProtectedBlockException(string key, string operation = "modify")
        : base($"Cannot {operation} protected block: \"{key}\"")
    {
        Key = key;
        Operation = operation;
    }

    /// <summary>The key of the protected block.</summary>
    public string Key { get; }

    /// <summary>The refused operation, for example <c>modify</c> or <c>remove</c>.</summary>
    public string Operation { get; }
}
