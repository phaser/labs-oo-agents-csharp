// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py (_extract_module_context) tests (intent)

using Nooa.Strategies;
using Nooa.Tests.Strategies.Acceptance;

namespace Nooa.Tests.Strategies;

public class CodeActScopeTests
{
    private static CurrentCall Call() => new()
    {
        MethodName = "M",
        ReturnType = typeof(List<OrderResult>),
        Arguments = [new CallArgument("map", typeof(Dictionary<string, FeedbackAnalysis[]>), null), new CallArgument("n", typeof(int), 1)],
    };

    [Fact]
    public void Types_CollectsAgentReturnAndArgumentTypesWithTheirTypeArguments()
    {
        var types = CodeActScope.Types(typeof(CodeActAgent), Call());

        Assert.Contains(typeof(CodeActAgent), types);
        Assert.Contains(typeof(OrderResult), types);
        Assert.Contains(typeof(FeedbackAnalysis), types);
        Assert.Contains(typeof(int), types);
    }

    [Fact]
    public void Namespaces_ExcludeTheDefaultsAndDuplicates()
    {
        var namespaces = CodeActScope.Namespaces(CodeActScope.Types(typeof(CodeActAgent), Call()));

        Assert.Equal(["Nooa.Tests.Strategies", "Nooa.Tests.Strategies.Acceptance"], namespaces);
    }

    [Fact]
    public void Assemblies_AreDistinctAndHaveFilePaths()
    {
        var assemblies = CodeActScope.Assemblies(CodeActScope.Types(typeof(CodeActAgent), Call()));

        Assert.Contains(typeof(CodeActAgent).Assembly, assemblies);
        Assert.Equal(assemblies.Count, assemblies.Distinct().Count());
        Assert.All(assemblies, a => Assert.NotEmpty(a.Location));
    }
}
