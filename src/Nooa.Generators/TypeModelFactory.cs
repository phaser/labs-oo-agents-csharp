// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nooa.Generators.Models;

namespace Nooa.Generators;

/// <summary>Builds a <see cref="TypeModel"/> from an agent class symbol.</summary>
internal static class TypeModelFactory
{
    public const string AgentOptionsAttributeName = "Nooa.AgentOptionsAttribute";

    public static TypeModel Create(INamedTypeSymbol type, Compilation compilation)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var location = LocationInfo.From(type.Locations.FirstOrDefault());
        var name = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        var containers = new List<ContainerModel>();
        var partial = IsPartialClass(type);
        for (var container = type.ContainingType; container is not null; container = container.ContainingType)
        {
            partial &= IsPartialClass(container);
            containers.Insert(0, new ContainerModel(Keyword(container) ?? "class", container.Name));
        }

        if (!partial)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.ClassNotPartial, location, name));
        }

        if (type.IsGenericType || type.ContainingType is { IsGenericType: true })
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.GenericNotSupported, location, name));
        }

        if (!IsAccessible(type))
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.TypeNotAccessible, location, name));
        }

        var typeComment = DocCommentReader.Read(type);
        PromptModel? systemPrompt = null;
        if (typeComment?.Text is { } text && diagnostics.Count == 0)
        {
            systemPrompt = PromptParser.Parse(text, path => ResolvePath(type, compilation, path), out var error);
            if (error is not null)
            {
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidPlaceholder, location, error));
            }
        }

        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        return new TypeModel(
            type.ToDisplayString(SymbolFormats.TypeOf),
            ns,
            new EquatableArray<ContainerModel>(containers),
            Keyword(type) ?? "class",
            type.Name,
            ReadLlm(type),
            systemPrompt,
            typeComment?.Text,
            new EquatableArray<MemberDocModel>(ReadMembers(type)),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    /// <summary>
    /// Compiles a member path such as <c>Config.Model</c> into an expression on the variable <c>a</c>. Returns null
    /// when a segment is not a readable instance field or property that the agent class can access.
    /// </summary>
    public static string? ResolvePath(INamedTypeSymbol agent, Compilation compilation, string path)
    {
        var expression = "a";
        ITypeSymbol? previous = null;
        foreach (var segment in path.Split('.'))
        {
            var current = previous ?? agent;
            ITypeSymbol? type = null;
            for (var t = current as INamedTypeSymbol; t is not null && type is null; t = t.BaseType)
            {
                foreach (var member in t.GetMembers(segment))
                {
                    if (!member.IsStatic && compilation.IsSymbolAccessibleWithin(member, agent))
                    {
                        if (member is IFieldSymbol field)
                        {
                            type = field.Type;
                        }
                        else if (member is IPropertySymbol { IsIndexer: false, GetMethod: not null } property)
                        {
                            type = property.Type;
                        }
                    }
                }
            }

            if (type is null)
            {
                return null;
            }

            var nullable = previous is not null
                && (previous.IsReferenceType || previous.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
            expression += (nullable ? "?." : ".") + SymbolFormats.Identifier(segment);
            previous = type;
        }

        return expression;
    }

    private static string? ReadLlm(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != AgentOptionsAttributeName)
            {
                continue;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Llm")
                {
                    return argument.Value.Value as string;
                }
            }
        }

        return null;
    }

    private static string? Keyword(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
        {
            return null;
        }

        return type.IsRecord ? "record" : "class";
    }

    private static bool IsPartialClass(INamedTypeSymbol type)
    {
        if (Keyword(type) is null)
        {
            return false;
        }

        return type.DeclaringSyntaxReferences.All(
            r => r.GetSyntax() is TypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private static bool IsAccessible(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal or Accessibility.NotApplicable))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<MemberDocModel> ReadMembers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared || member.DeclaringSyntaxReferences.Length == 0)
            {
                continue;
            }

            switch (member)
            {
                case IFieldSymbol { AssociatedSymbol: null } field when DocCommentReader.Read(field) is { Text: not null } comment:
                    yield return new MemberDocModel("Field", field.Name, EquatableArray<string>.Empty, comment.Text, EquatableArray<ParamDocModel>.Empty);
                    break;
                case IPropertySymbol { IsIndexer: false, ExplicitInterfaceImplementations.Length: 0 } property when DocCommentReader.Read(property) is { Text: not null } comment:
                    yield return new MemberDocModel("Property", property.Name, EquatableArray<string>.Empty, comment.Text, EquatableArray<ParamDocModel>.Empty);
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary, TypeParameters.Length: 0, ExplicitInterfaceImplementations.Length: 0 } method
                    when method.Parameters.All(p => p.RefKind == RefKind.None && SymbolFormats.IsReflectable(p.Type)):
                    var methodDoc = ReadMethod(method);
                    if (methodDoc is not null)
                    {
                        yield return methodDoc;
                    }

                    break;
            }
        }
    }

    private static MemberDocModel? ReadMethod(IMethodSymbol method)
    {
        var comment = DocCommentReader.Read(method);
        if (comment is null)
        {
            return null;
        }

        var parameters = new List<ParamDocModel>();
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var doc = comment.Params.FirstOrDefault(p => p.Name == method.Parameters[i].Name);
            if (doc is not null)
            {
                parameters.Add(new ParamDocModel(i, doc.Text));
            }
        }

        if (comment.Returns is not null)
        {
            parameters.Add(new ParamDocModel(-1, comment.Returns));
        }

        var types = method.Parameters.Select(p => "typeof(" + p.Type.ToDisplayString(SymbolFormats.TypeOf) + ")");
        return new MemberDocModel("Method", method.Name, new EquatableArray<string>(types), comment.Text, new EquatableArray<ParamDocModel>(parameters));
    }
}
