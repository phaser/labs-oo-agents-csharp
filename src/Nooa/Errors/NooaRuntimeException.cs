// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/__init__.py

namespace Nooa.Errors;

/// <summary>
/// Error in the agent runtime system.
/// </summary>
public class NooaRuntimeException : NooaException
{
    public NooaRuntimeException(string message)
        : base(message)
    {
    }

    public NooaRuntimeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
