// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace Nooa.Generators;

/// <summary>The diagnostics of the agent generator. The ids are stable.</summary>
internal static class DiagnosticDescriptors
{
    private const string _category = "Nooa";

    public static readonly DiagnosticDescriptor ClassNotPartial = Create(
        "NOOA0001",
        "Agent class must be partial",
        "'{0}' must be a partial class, and so must every type that contains it");

    public static readonly DiagnosticDescriptor ReturnTypeNotAwaitable = Create(
        "NOOA0002",
        "Agentic method must return a task",
        "Method '{0}' must return Task, Task<T>, ValueTask or ValueTask<T>");

    public static readonly DiagnosticDescriptor StaticNotSupported = Create(
        "NOOA0003",
        "Agentic method must not be static",
        "Method '{0}' must be an instance method");

    public static readonly DiagnosticDescriptor GenericNotSupported = Create(
        "NOOA0004",
        "Generic agent types and methods are not supported",
        "'{0}' is generic. Generic agent classes and generic agentic methods are not supported");

    public static readonly DiagnosticDescriptor ByRefParameter = Create(
        "NOOA0005",
        "Agentic method parameters must be passed by value",
        "Method '{0}' has a ref, out, in, pointer or dynamic parameter, which is not supported");

    public static readonly DiagnosticDescriptor MethodNotPartial = Create(
        "NOOA0006",
        "Agentic method must be a partial declaration without a body",
        "Method '{0}' must be declared 'partial' and have no body. The generator emits the body");

    public static readonly DiagnosticDescriptor InvalidPlaceholder = Create(
        "NOOA0007",
        "Invalid placeholder in a doc comment",
        "{0}");

    public static readonly DiagnosticDescriptor MissingTarget = Create(
        "NOOA0008",
        "Agent class must implement IAgenticTarget",
        "'{0}' has [Agentic] methods, so it must implement Nooa.IAgenticTarget (derive from Agent)");

    public static readonly DiagnosticDescriptor TypeNotAccessible = Create(
        "NOOA0009",
        "Agent class must be public or internal",
        "'{0}' and the types that contain it must be public or internal");

    private static DiagnosticDescriptor Create(string id, string title, string message)
    {
        return new DiagnosticDescriptor(id, title, message, _category, DiagnosticSeverity.Error, isEnabledByDefault: true);
    }
}
