// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis.CSharp;

namespace Nooa.Runtime;

/// <summary>
/// An argument of the agent method that the cell runs in. The engine declares it as a typed variable,
/// so the model code sees <c>List&lt;string&gt; items</c> and not <c>object</c>.
/// </summary>
/// <param name="Name">The variable name. It must be a valid C# identifier.</param>
/// <param name="Type">The declared type. It must be public so the script can name it.</param>
/// <param name="Value">The value. It must be assignable to <paramref name="Type"/>.</param>
public sealed record ReplArgument(string Name, Type Type, object? Value)
{
    /// <summary>The name as it appears in a declaration: a keyword gets the <c>@</c> prefix.</summary>
    internal string Identifier => SyntaxFacts.GetKeywordKind(Name) == SyntaxKind.None ? Name : "@" + Name;
}
