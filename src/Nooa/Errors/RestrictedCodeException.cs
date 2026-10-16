// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/__init__.py

namespace Nooa.Errors;

/// <summary>
/// Generated code uses restricted or forbidden features.
/// The message explains what is forbidden and suggests alternatives.
/// </summary>
public class RestrictedCodeException : ValidationException
{
    public RestrictedCodeException(string message, int? lineNumber = null, Exception? innerException = null)
        : base(message, innerException!)
    {
        LineNumber = lineNumber;
    }

    /// <summary>The line of the generated code that broke the rule, when known.</summary>
    public int? LineNumber { get; }
}
