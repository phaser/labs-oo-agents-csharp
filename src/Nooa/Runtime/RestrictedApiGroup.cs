// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Runtime;

/// <summary>A group of APIs that <see cref="CellCodeValidator"/> refuses by default. An agent opts in to a group.</summary>
[Flags]
public enum RestrictedApiGroup
{
    /// <summary>No group.</summary>
    None = 0,

    /// <summary>Files and directories: <c>File</c>, <c>Directory</c>, <c>FileStream</c>, memory-mapped files, zip files on disk.</summary>
    FileSystem = 1,

    /// <summary>Network and database clients: <c>System.Net</c>, <c>System.Data</c>, <c>Microsoft.Data</c>.</summary>
    Network = 2,

    /// <summary>Processes, the console and the debugger: <c>Process</c>, <c>Environment.Exit</c>, <c>Console</c>, <c>Debugger</c>.</summary>
    Process = 4,

    /// <summary>The environment of the host: <c>Environment</c> variables, folders and the current directory.</summary>
    Environment = 8,

    /// <summary>Reflection and dynamic code: <c>Type</c>, <c>Activator</c>, <c>Assembly</c>, <c>AppDomain</c>, <c>dynamic</c>, Roslyn.</summary>
    Reflection = 16,

    /// <summary>Native code and memory: <c>Marshal</c>, P/Invoke, <c>Unsafe</c>, <c>unsafe</c>, <c>extern</c>, pointers.</summary>
    NativeCode = 32,
}
