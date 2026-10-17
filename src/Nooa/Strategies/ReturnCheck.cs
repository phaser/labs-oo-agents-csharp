// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (_handle_return_result)

namespace Nooa.Strategies;

/// <summary>The outcome of a check of a returned value: the typed value, or the feedback for the model.</summary>
/// <param name="Value">The value with the type of the method result. Null when the check failed or the result is empty.</param>
/// <param name="Error">The feedback for the model, or null when the value is valid.</param>
internal sealed record ReturnCheck(object? Value, string? Error)
{
    /// <summary>True when the value passed every check.</summary>
    public bool IsValid => Error is null;

    public static ReturnCheck Ok(object? value) => new(value, null);

    public static ReturnCheck Fail(string error) => new(null, error);
}
