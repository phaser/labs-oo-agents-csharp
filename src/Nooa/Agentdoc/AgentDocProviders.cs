// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>
/// Registry of <see cref="IAgentDocProvider"/> instances. <see cref="Default"/> asks the registered providers in
/// registration order, then falls back to <see cref="ReflectionDocProvider"/>. The first non-blank answer wins.
/// </summary>
public static class AgentDocProviders
{
    private static readonly Lock _gate = new();
    private static IAgentDocProvider[] _registered = [];

    /// <summary>The provider that <c>AgentDocs.Doc</c> uses when the caller passes none.</summary>
    public static IAgentDocProvider Default { get; } = new ChainProvider();

    /// <summary>Adds a provider. Dispose the result to remove it again.</summary>
    public static IDisposable Register(IAgentDocProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_gate)
        {
            _registered = [.. _registered, provider];
        }

        return new Registration(provider);
    }

    private sealed class Registration(IAgentDocProvider provider) : IDisposable
    {
        public void Dispose()
        {
            lock (_gate)
            {
                _registered = [.. _registered.Where(p => !ReferenceEquals(p, provider))];
            }
        }
    }

    private sealed class ChainProvider : IAgentDocProvider
    {
        public string? GetSummary(MemberInfo member)
        {
            foreach (var provider in Volatile.Read(ref _registered))
            {
                var text = provider.GetSummary(member);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return ReflectionDocProvider.Instance.GetSummary(member);
        }

        public string? GetParameterSummary(ParameterInfo parameter)
        {
            foreach (var provider in Volatile.Read(ref _registered))
            {
                var text = provider.GetParameterSummary(parameter);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return ReflectionDocProvider.Instance.GetParameterSummary(parameter);
        }
    }
}
