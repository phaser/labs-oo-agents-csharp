// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/method_wrapper.py

using System.Collections.Concurrent;
using System.Reflection;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// Finds the C# method behind a <see cref="CurrentCall"/>, to read the attributes that the call does not carry.
/// The match is by name and by the ordered parameter names, without a trailing <see cref="CancellationToken"/>.
/// </summary>
internal static class AgentMethodLookup
{
    private const BindingFlags _all =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type, string, string), MethodInfo?> _cache = new();

    public static MethodInfo? Find(Type agentType, CurrentCall call)
    {
        var names = string.Join(',', call.Arguments.Select(a => a.Name));
        return _cache.GetOrAdd((agentType, call.MethodName, names), static key => Search(key.Item1, key.Item2, key.Item3));
    }

    private static MethodInfo? Search(Type agentType, string methodName, string names)
    {
        for (var type = agentType; type is not null && type != typeof(object); type = type.BaseType)
        {
            foreach (var method in type.GetMethods(_all))
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                var parameters = method.GetParameters().AsEnumerable();
                if (parameters.LastOrDefault()?.ParameterType == typeof(CancellationToken))
                {
                    parameters = parameters.SkipLast(1);
                }

                if (string.Join(',', parameters.Select(p => p.Name)) == names)
                {
                    return method;
                }
            }
        }

        return null;
    }
}
