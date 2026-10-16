// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nooa.Generators.Tests;

/// <summary>Runs <see cref="AgentGenerator"/> over a source snippet, in memory.</summary>
internal static class GeneratorHost
{
    /// <summary>Global usings that every snippet gets.</summary>
    private const string _usings = """
        global using System.Threading;
        global using System.Threading.Tasks;
        global using Nooa;
        global using Nooa.Strategies;
        """;

    /// <summary>Declares <c>Base</c>, a minimal <c>IAgenticTarget</c> that records the last call in <c>Base.Last</c>.</summary>
    public const string Prelude = """
        public abstract class Base : IAgenticTarget
        {
            public static CurrentCall? Last;
            public static CancellationToken LastToken;

            public Task<T> InvokeAgenticAsync<T>(CurrentCall call, CancellationToken cancellationToken)
            {
                Last = call;
                LastToken = cancellationToken;
                return Task.FromResult(default(T)!);
            }

            public Task InvokeAgenticAsync(CurrentCall call, CancellationToken cancellationToken)
            {
                Last = call;
                LastToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        """;

    private static readonly ImmutableArray<MetadataReference> _references = LoadReferences();

    public static Result Run(string source, DocumentationMode documentation = DocumentationMode.None, bool withPrelude = true, string? assemblyName = null)
    {
        var compilation = CreateCompilation(source, documentation, withPrelude, assemblyName);
        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var sources = driver.GetRunResult().Results[0].GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        return new Result(sources, diagnostics, output, output.GetDiagnostics());
    }

    public static CSharpCompilation CreateCompilation(string source, DocumentationMode documentation = DocumentationMode.None, bool withPrelude = true, string? assemblyName = null)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, documentation);
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(_usings, options), CSharpSyntaxTree.ParseText(source, options, path: "Snippet.cs") };
        if (withPrelude)
        {
            trees.Add(CSharpSyntaxTree.ParseText(Prelude, options));
        }

        return CSharpCompilation.Create(
            assemblyName ?? "Fixture" + Guid.NewGuid().ToString("N"),
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorDriver CreateDriver()
    {
        return CSharpGeneratorDriver.Create(
            [new AgentGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    /// <summary>Compiles the output and loads it. Fails the test when the output has errors.</summary>
    public static Assembly Load(Result result)
    {
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        using var stream = new MemoryStream();
        var emit = result.Output.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var paths = trusted.Split(Path.PathSeparator).Append(typeof(AgenticAttribute).Assembly.Location);
        return [.. paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))];
    }

    internal sealed record Result(
        IReadOnlyDictionary<string, string> Sources,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        Compilation Output,
        ImmutableArray<Diagnostic> CompilationDiagnostics)
    {
        public IEnumerable<string> GeneratorIds => GeneratorDiagnostics.Select(d => d.Id);

        public string Generated => Sources.Single(s => s.Key.EndsWith(".Nooa.g.cs", StringComparison.Ordinal)).Value;
    }
}
