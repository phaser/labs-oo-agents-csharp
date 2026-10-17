// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/code_validator.py

using System.Reflection;

namespace Nooa.Runtime;

/// <summary>Settings of a <see cref="CellCodeValidator"/>.</summary>
public sealed record CellValidatorOptions
{
    /// <summary>The groups of restricted APIs that the agent may use. The default is none.</summary>
    public RestrictedApiGroup Allow { get; init; } = RestrictedApiGroup.None;

    /// <summary>
    /// The type of the globals object of the engine, for example <c>ReplGlobals&lt;MyAgent&gt;</c>.
    /// The validator needs it to bind <c>self</c> and the other globals. Null means no globals.
    /// </summary>
    public Type? GlobalsType { get; init; }

    /// <summary>
    /// The names of the methods that a cell must not call on <c>self</c>. Use it for the method that the model is running now,
    /// to stop endless recursion (upstream: <c>forbidden_self_calls</c>).
    /// </summary>
    public IReadOnlyCollection<string> ForbiddenSelfCalls { get; init; } = [];

    /// <summary>
    /// The return type of the running method. A cell must not declare a variable or a local function that has the name of this type
    /// or of one of its type arguments, because the name would hide the type.
    /// </summary>
    public Type? ReturnType { get; init; }

    /// <summary>Assemblies that the engine references in addition to the defaults. Give the same list as <see cref="ReplOptions.References"/>.</summary>
    public IReadOnlyList<Assembly> References { get; init; } = [];

    /// <summary>Namespaces that the engine imports in addition to the defaults. Give the same list as <see cref="ReplOptions.Imports"/>.</summary>
    public IReadOnlyList<string> Imports { get; init; } = [];
}
