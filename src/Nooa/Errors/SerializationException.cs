// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/storage.py

namespace Nooa.Errors;

/// <summary>
/// Error serializing agent state for a snapshot.
/// Raised when a context-block value cannot be serialized. A non-serializable agent member is not fatal:
/// it is logged and skipped.
/// </summary>
public class SerializationException : NooaException
{
    public SerializationException(string message)
        : base(message)
    {
    }

    public SerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
