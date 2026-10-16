// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nooa.Generators.Models;

namespace Nooa.Generators;

/// <summary>Validates an <c>[Agentic]</c> method and builds its <see cref="MethodModel"/>.</summary>
internal static class MethodModelFactory
{
    private const string _strategyAttributeName = "Nooa.StrategyAttribute";
    private const string _targetInterfaceName = "Nooa.IAgenticTarget";
    private const string _cancellationTokenName = "System.Threading.CancellationToken";

    public static MethodResult Create(IMethodSymbol method, Compilation compilation)
    {
        var type = method.ContainingType;
        var typeModel = TypeModelFactory.Create(type, compilation);
        var location = LocationInfo.From(method.Locations.FirstOrDefault());
        var name = method.Name;
        var diagnostics = new List<DiagnosticInfo>();

        if (method.IsStatic)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.StaticNotSupported, location, name));
        }

        if (method.TypeParameters.Length > 0)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.GenericNotSupported, location, name));
        }

        var returnKind = ClassifyReturn(method.ReturnType);
        if (returnKind == ReturnKind.None)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.ReturnTypeNotAwaitable, location, name));
        }

        if (method.Parameters.Any(p => p.RefKind != RefKind.None || !SymbolFormats.IsReflectable(p.Type)))
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.ByRefParameter, location, name));
        }

        if (!method.IsPartialDefinition || method.PartialImplementationPart is not null)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MethodNotPartial, location, name));
        }

        if (!type.AllInterfaces.Any(i => i.ToDisplayString() == _targetInterfaceName))
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MissingTarget, LocationInfo.From(type.Locations.FirstOrDefault()), type.Name));
        }

        PromptModel? prompt = null;
        if (DocCommentReader.Read(method)?.Text is { } text)
        {
            prompt = PromptParser.Parse(text, path => TypeModelFactory.ResolvePath(type, compilation, path), out var error);
            if (error is not null)
            {
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidPlaceholder, location, error));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new MethodResult(typeModel, null, new EquatableArray<DiagnosticInfo>(diagnostics));
        }

        return new MethodResult(typeModel, Build(method, returnKind, prompt), EquatableArray<DiagnosticInfo>.Empty);
    }

    private static MethodModel Build(IMethodSymbol method, ReturnKind returnKind, PromptModel? prompt)
    {
        var parameters = method.Parameters;
        string? cancellationToken = null;
        if (parameters.Length > 0 && parameters[parameters.Length - 1].Type.ToDisplayString() == _cancellationTokenName)
        {
            cancellationToken = SymbolFormats.Identifier(parameters[parameters.Length - 1].Name);
            parameters = parameters.RemoveAt(parameters.Length - 1);
        }

        var models = method.Parameters.Select(p => new ParameterModel(
            SymbolFormats.Identifier(p.Name),
            (p.IsParams ? "params " : string.Empty) + p.Type.ToDisplayString(SymbolFormats.Declared),
            p.Type.ToDisplayString(SymbolFormats.TypeOf),
            p.Type.ToDisplayString(SymbolFormats.Short) + " " + p.Name)).ToList();
        var visible = new EquatableArray<ParameterModel>(models.Take(parameters.Length));

        var withResult = returnKind is ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;
        var result = withResult ? ((INamedTypeSymbol)method.ReturnType).TypeArguments[0] : null;
        var signature = "(" + string.Join(", ", parameters.Select(p => p.Type.ToDisplayString(SymbolFormats.Short) + " " + p.Name))
            + ") -> " + (result?.ToDisplayString(SymbolFormats.Short) ?? "void");

        var syntax = (MethodDeclarationSyntax)method.DeclaringSyntaxReferences[0].GetSyntax();
        var modifiers = string.Join(" ", syntax.Modifiers.Select(m => m.Text).Where(m => m != "partial"));

        // The signature part of the emitted method keeps every parameter, including the token.
        return new MethodModel(
            method.Name,
            modifiers,
            method.ReturnType.ToDisplayString(SymbolFormats.Declared),
            withResult,
            result?.ToDisplayString(SymbolFormats.Declared),
            result?.ToDisplayString(SymbolFormats.TypeOf),
            new EquatableArray<ParameterModel>(models),
            cancellationToken,
            prompt,
            signature,
            ReadStrategy(method),
            visible);
    }

    private static string? ReadStrategy(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == _strategyAttributeName
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is ITypeSymbol strategy)
            {
                return strategy.ToDisplayString(SymbolFormats.TypeOf);
            }
        }

        return null;
    }

    private static ReturnKind ClassifyReturn(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { ContainingNamespace: { } ns } named || ns.ToDisplayString() != "System.Threading.Tasks")
        {
            return ReturnKind.None;
        }

        return (named.Name, named.Arity) switch
        {
            ("Task", 0) => ReturnKind.Task,
            ("Task", 1) => ReturnKind.TaskOfT,
            ("ValueTask", 0) => ReturnKind.ValueTask,
            ("ValueTask", 1) => ReturnKind.ValueTaskOfT,
            _ => ReturnKind.None,
        };
    }

    private enum ReturnKind
    {
        None,
        Task,
        TaskOfT,
        ValueTask,
        ValueTaskOfT,
    }
}
