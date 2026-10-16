// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Nooa.Generators.Tests;

public class DiagnosticDescriptorsTests
{
    [Fact]
    public void Descriptors_HaveStableSequentialIds_AndAreErrors()
    {
        var descriptors = typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Enumerable.Range(1, 9).Select(i => $"NOOA{i:D4}"), descriptors.Select(d => d.Id));
        Assert.All(descriptors, d => Assert.Equal(DiagnosticSeverity.Error, d.DefaultSeverity));
    }

    [Fact]
    public void ReleaseTrackingFile_ListsEveryId()
    {
        var file = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "AnalyzerReleases.Unshipped.md"));

        Assert.All(Enumerable.Range(1, 9), i => Assert.Contains($"NOOA{i:D4}", file, StringComparison.Ordinal));
    }
}
