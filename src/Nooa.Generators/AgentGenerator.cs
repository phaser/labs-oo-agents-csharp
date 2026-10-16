// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nooa.Generators.Models;

namespace Nooa.Generators;

/// <summary>
/// Emits the bodies of <c>[Agentic]</c> partial methods and the per-type metadata of agent classes: a doc provider
/// and an <c>AgentMetadata</c> record. It is incremental. Its models hold no symbols.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AgentGenerator : IIncrementalGenerator
{
    private const string _agenticAttributeName = "Nooa.AgenticAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methods = context.SyntaxProvider.ForAttributeWithMetadataName(
            _agenticAttributeName,
            static (node, _) => node is MethodDeclarationSyntax,
            static (ctx, _) => MethodModelFactory.Create((IMethodSymbol)ctx.TargetSymbol, ctx.SemanticModel.Compilation));

        var types = context.SyntaxProvider.ForAttributeWithMetadataName(
            TypeModelFactory.AgentOptionsAttributeName,
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            static (ctx, _) => TypeModelFactory.Create((INamedTypeSymbol)ctx.TargetSymbol, ctx.SemanticModel.Compilation));

        var input = methods.Collect().Combine(types.Collect()).Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Assembly"));
        context.RegisterSourceOutput(input, static (spc, source) => Generate(spc, source.Left.Left, source.Left.Right, source.Right));
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<MethodResult> methods, ImmutableArray<TypeModel> types, string assemblyName)
    {
        var groups = new Dictionary<string, (TypeModel Type, List<MethodModel> Methods)>();
        var reported = new HashSet<DiagnosticInfo>();

        void Report(DiagnosticInfo diagnostic)
        {
            if (reported.Add(diagnostic))
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
        }

        foreach (var type in types)
        {
            groups[type.Key] = (type, new List<MethodModel>());
        }

        foreach (var result in methods)
        {
            if (!groups.ContainsKey(result.Type.Key))
            {
                groups[result.Type.Key] = (result.Type, new List<MethodModel>());
            }

            foreach (var diagnostic in result.Diagnostics)
            {
                Report(diagnostic);
            }

            if (result.Method is not null)
            {
                groups[result.Type.Key].Methods.Add(result.Method);
            }
        }

        var emitted = new List<TypeModel>();
        foreach (var (type, typeMethods) in groups.Values.OrderBy(g => g.Type.Key, System.StringComparer.Ordinal))
        {
            foreach (var diagnostic in type.Diagnostics)
            {
                Report(diagnostic);
            }

            if (type.Diagnostics.Count > 0)
            {
                continue;
            }

            emitted.Add(type);
            context.AddSource(SourceEmitter.HintName(type), SourceText.From(SourceEmitter.EmitType(type, typeMethods), Encoding.UTF8));
        }

        if (emitted.Count > 0)
        {
            context.AddSource("Nooa.ModuleInitializer.g.cs", SourceText.From(SourceEmitter.EmitRegistration(assemblyName, emitted), Encoding.UTF8));
        }
    }
}
