// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/storage.py

namespace Nooa.Errors;

/// <summary>
/// Error deserializing agent state from a snapshot.
/// Raised when a stored value cannot be restored, for example when the envelope names a type that is not in the allowlist
/// or the data does not match the expected format.
/// </summary>
public class DeserializationException : NooaException
{
    public DeserializationException(string message)
        : base(message)
    {
    }

    public DeserializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
