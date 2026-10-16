// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/core.py

using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>
/// Renders prompt-ready API documentation for agent types, instances and methods (upstream <c>doc()</c>).
/// </summary>
public static class AgentDocs
{
    /// <summary>Documents one object: a <see cref="Type"/>, a <see cref="MethodInfo"/> or an instance.</summary>
    /// <param name="obj">A type shows its declared API. An instance shows the same API with current values.</param>
    /// <param name="concise">Show only the first line of each doc comment.</param>
    /// <param name="inlineDepth">Levels of referenced types to expand below the result. 0 means none.</param>
    /// <param name="provider">Source of doc comment text. Null uses <see cref="AgentDocProviders.Default"/>.</param>
    public static string Doc(object obj, bool concise = false, int inlineDepth = 1, IAgentDocProvider? provider = null)
        => Doc([obj], concise, inlineDepth, provider);

    /// <summary>
    /// Documents several objects. Each referenced type appears once, under one "## Referenced Types" heading.
    /// </summary>
    public static string Doc(IEnumerable<object> objs, bool concise = false, int inlineDepth = 1, IAgentDocProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(objs);
        var list = objs.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Doc requires at least one object.", nameof(objs));
        }

        if (list.Contains(null!))
        {
            throw new ArgumentException("Doc cannot document null.", nameof(objs));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(inlineDepth);

        provider ??= AgentDocProviders.Default;
        var formatter = new DocFormatter(provider, concise);
        var sections = new List<string>();
        var seen = new HashSet<Type>();
        var seeds = new List<IReadOnlySet<Type>>();

        foreach (var obj in list)
        {
            switch (obj)
            {
                case Type type:
                    sections.Add(formatter.FormatType(type));
                    seen.Add(ReferencedTypes.Normalize(type));
                    seeds.Add(ReferencedTypes.Discover(type));
                    break;
                case MethodInfo method:
                    sections.Add(formatter.FormatMethod(method));
                    seeds.Add(ReferencedTypes.Discover(method));
                    break;
                default:
                    var instanceType = obj.GetType();
                    if (ReferencedTypes.IsCustom(ReferencedTypes.Normalize(instanceType)))
                    {
                        sections.Add(formatter.FormatType(instanceType, obj));
                        seen.Add(ReferencedTypes.Normalize(instanceType));
                        seeds.Add(ReferencedTypes.Discover(instanceType));
                    }
                    else
                    {
                        sections.Add(ValuePreview.Format(obj));
                    }

                    break;
            }
        }

        if (inlineDepth > 0)
        {
            var references = ReferencedTypes.Collect(seeds, seen, inlineDepth);
            if (references.Count > 0)
            {
                var conciseFormatter = new DocFormatter(provider, concise: true);
                sections.Add("## Referenced Types\n\n" + string.Join("\n\n", references.Select(t => conciseFormatter.FormatType(t))));
            }
        }

        return string.Join("\n\n", sections);
    }
}
