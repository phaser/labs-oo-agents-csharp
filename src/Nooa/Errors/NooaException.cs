// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/__init__.py

namespace Nooa.Errors;

/// <summary>
/// Base exception for all Nooa errors.
/// </summary>
public class NooaException : Exception
{
    public NooaException(string message)
        : base(message)
    {
    }

    public NooaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
