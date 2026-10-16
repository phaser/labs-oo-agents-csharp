// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Errors;

/// <summary>
/// A configuration record holds invalid values. The message lists every problem, one per line.
/// </summary>
public class ConfigValidationException : NooaException
{
    public ConfigValidationException(string message)
        : base(message)
    {
    }
}
