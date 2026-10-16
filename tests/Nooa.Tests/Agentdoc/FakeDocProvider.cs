// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

/// <summary>Test double for the doc comment seam. T11 will emit a generated equivalent.</summary>
internal sealed class FakeDocProvider : IAgentDocProvider
{
    public Dictionary<MemberInfo, string> Summaries { get; } = [];

    public Dictionary<ParameterInfo, string> Parameters { get; } = [];

    public string? GetSummary(MemberInfo member) => Summaries.GetValueOrDefault(member);

    public string? GetParameterSummary(ParameterInfo parameter) => Parameters.GetValueOrDefault(parameter);
}
