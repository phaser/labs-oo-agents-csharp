// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (_extract_module_context)

using System.Reflection;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// The names that a cell sees for one call: the namespaces and assemblies of the agent type, the return type and the argument
/// types. Upstream built the same scope from the module of the agent class. The engine, the validator and the execution
/// context block use one list, so the prompt shows what the cell can use.
/// </summary>
internal static class CodeActScope
{
    /// <summary>The types that the scope comes from: the agent, the return type and the argument types, with their type arguments.</summary>
    public static IReadOnlyList<Type> Types(Type agentType, CurrentCall call)
    {
        var types = new List<Type>();
        Collect(agentType, types);
        if (call.ReturnType is { } returnType)
        {
            Collect(returnType, types);
        }

        foreach (var argument in call.Arguments)
        {
            Collect(argument.Type, types);
        }

        return types;
    }

    /// <summary>The namespaces to import in addition to the defaults, in a stable order.</summary>
    public static IReadOnlyList<string> Namespaces(IEnumerable<Type> types) =>
        [.. types
            .Select(t => t.Namespace)
            .OfType<string>()
            .Where(n => n.Length > 0)
            .Except(RoslynExecutionEngine.DefaultImports, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>The assemblies to reference in addition to the defaults. Assemblies without a file path are left out.</summary>
    public static IReadOnlyList<Assembly> Assemblies(IEnumerable<Type> types) =>
        [.. types.Select(t => t.Assembly).Where(a => !a.IsDynamic && a.Location.Length > 0).Distinct()];

    private static void Collect(Type type, List<Type> types)
    {
        if (type.HasElementType)
        {
            Collect(type.GetElementType()!, types);
            return;
        }

        types.Add(type);
        foreach (var argument in type.GenericTypeArguments)
        {
            Collect(argument, types);
        }
    }
}
