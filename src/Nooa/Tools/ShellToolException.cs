// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tools;

/// <summary>
/// A shell or file operation failed, or a call used the shell tools in a wrong way. The message says how to correct the call.
/// </summary>
public class ShellToolException : NooaException
{
    public ShellToolException(string message)
        : base(message)
    {
    }

    public ShellToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
