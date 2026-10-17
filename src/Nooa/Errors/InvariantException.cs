// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategy_validation.py

namespace Nooa.Errors;

/// <summary>
/// A model-correctable method invariant failure. A postcondition throws it to reject a result that has the
/// right type. Strategies route it through their validation-retry feedback. A precondition runs before
/// generation and fails fast. Any other exception from a condition is an infrastructure error.
/// </summary>
public class InvariantException : ValidationException
{
    public InvariantException(string message)
        : base(message)
    {
    }

    public InvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
