// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/resolved.py

namespace Nooa.Config;

/// <summary>
/// A secret text value that does not print. It stands in for <c>pydantic.SecretStr</c>.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> returns a mask. The type has no public property, so a JSON serializer writes an empty object.
/// Call <see cref="GetSecretValue"/> to read the plain text.
/// </remarks>
public sealed class SecretValue
{
    private const string _mask = "**********";

    private readonly string _value;

    /// <summary>Wraps <paramref name="value"/>.</summary>
    public SecretValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Returns the plain text.</summary>
    public string GetSecretValue() => _value;

    /// <summary>Returns a mask, never the value.</summary>
    public override string ToString() => _mask;
}
