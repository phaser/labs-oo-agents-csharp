// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/code_validator.py

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nooa.Runtime;

/// <summary>
/// Walks the syntax tree of a cell and collects the issues. It checks the symbols that the semantic model binds, so an alias,
/// <c>global::</c>, <c>using static</c> and a full name give the same result as the short name.
/// </summary>
internal sealed class CellValidationWalker(SemanticModel model, CellValidatorOptions options, IReadOnlySet<string> protectedNames)
    : CSharpSyntaxWalker(SyntaxWalkerDepth.StructuredTrivia)
{
    private readonly List<CellIssue> _issues = [];

    public IReadOnlyList<CellIssue> Issues => _issues;

    public override void Visit(SyntaxNode? node)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax or DelegateDeclarationSyntax:
                Report(node, "type declaration", "A cell must not declare a type. Use tuples, anonymous types, local functions or the types that already exist.");
                break;
            case PointerTypeSyntax or FunctionPointerTypeSyntax or UnsafeStatementSyntax or FixedStatementSyntax:
                ReportGroup(RestrictedApiGroup.NativeCode, node, "unsafe code", "Unsafe code and pointers are not allowed in cells. Use managed code only.");
                break;
            case InvocationExpressionSyntax or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax when model.GetTypeInfo(node).Type?.TypeKind == TypeKind.Dynamic:
                ReportGroup(RestrictedApiGroup.Reflection, node, "dynamic", "Dynamic binding is not allowed in cells. Use static types.");
                break;
        }

        base.Visit(node);
    }

    public override void VisitToken(SyntaxToken token)
    {
        switch (token.Kind())
        {
            case SyntaxKind.UnsafeKeyword or SyntaxKind.FixedKeyword:
                ReportGroup(RestrictedApiGroup.NativeCode, token, "unsafe code", "Unsafe code and pointers are not allowed in cells. Use managed code only.");
                break;
            case SyntaxKind.ExternKeyword:
                ReportGroup(RestrictedApiGroup.NativeCode, token, "extern", "A native method (extern or P/Invoke) is not allowed in cells. Use managed code only.");
                break;
        }

        base.VisitToken(token);
    }

    public override void VisitPragmaWarningDirectiveTrivia(PragmaWarningDirectiveTriviaSyntax node)
    {
        if (node.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
        {
            Report(node, "pragma warning disable", "A cell must not disable warnings. Fix the cause of the warning, for example add await.");
        }

        base.VisitPragmaWarningDirectiveTrivia(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        CheckName(node, node.Identifier.ValueText);
        base.VisitIdentifierName(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        CheckName(node, node.Identifier.ValueText);
        base.VisitGenericName(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "self" } self } access
            && options.ForbiddenSelfCalls.Contains(access.Name.Identifier.ValueText)
            && model.GetSymbolInfo(self).Symbol is null or IPropertySymbol or IFieldSymbol)
        {
            var name = access.Name.Identifier.ValueText;
            Report(node, "self." + name, $"Calling self.{name}() from its own cell is forbidden. It would cause infinite recursion.");
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        CheckConstructor(node);
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        CheckConstructor(node);
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        CheckShadow(node.Identifier);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        CheckShadow(node.Identifier);
        base.VisitVariableDeclarator(node);
    }

    public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
    {
        CheckShadow(node.Identifier);
        base.VisitSingleVariableDesignation(node);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        CheckShadow(node.Identifier);
        base.VisitLocalFunctionStatement(node);
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Alias is { } alias)
        {
            CheckShadow(alias.Name.Identifier);
        }

        base.VisitUsingDirective(node);
    }

    private void CheckConstructor(BaseObjectCreationExpressionSyntax node)
    {
        if (model.GetSymbolInfo(node).Symbol is { } constructor && RestrictedApiCatalog.Find(constructor, options.Allow) is { } rule)
        {
            Report(node, rule);
        }
    }

    private void CheckName(SimpleNameSyntax node, string text)
    {
        SyntaxNode place = node.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == node => access,
            QualifiedNameSyntax qualified when qualified.Right == node => qualified,
            _ => node,
        };
        var info = model.GetSymbolInfo(node);
        var symbols = info.Symbol is { } one ? [one] : info.CandidateSymbols;
        foreach (var symbol in symbols)
        {
            if (symbol is IDynamicTypeSymbol)
            {
                ReportGroup(RestrictedApiGroup.Reflection, node, "dynamic", "Dynamic binding is not allowed in cells. Use static types.");
            }
            else if (RestrictedApiCatalog.Find(symbol, options.Allow) is { } rule)
            {
                Report(place, rule);
            }
        }

        if (symbols.IsEmpty && RestrictedApiCatalog.FindUnresolved(text, options.Allow) is { } unresolved)
        {
            Report(place, unresolved);
        }
    }

    private void CheckShadow(SyntaxToken identifier)
    {
        var name = identifier.ValueText;
        if (protectedNames.Contains(name))
        {
            Report(identifier, "shadow " + name, $"Cannot declare '{name}' here. It is the return type of the method, and a local name hides it. Use the existing '{name}' directly.");
        }
    }

    private void ReportGroup(RestrictedApiGroup group, SyntaxNode node, string key, string message)
    {
        if ((options.Allow & group) != group)
        {
            Report(node, key, message);
        }
    }

    private void ReportGroup(RestrictedApiGroup group, SyntaxToken token, string key, string message)
    {
        if ((options.Allow & group) != group)
        {
            Report(token, key, message);
        }
    }

    private void Report(SyntaxNode node, RestrictedApiRule rule) => Report(node, rule.Label, $"{rule.Label} is restricted. {rule.Reason} {rule.Hint}");

    private void Report(SyntaxNode node, string key, string message) => Add(node.GetLocation(), key, message);

    private void Report(SyntaxToken token, string key, string message) => Add(token.GetLocation(), key, message);

    private void Add(Location location, string key, string message)
    {
        var span = location.GetLineSpan();
        var length = span.StartLinePosition.Line == span.EndLinePosition.Line ? span.EndLinePosition.Character - span.StartLinePosition.Character : 0;
        _issues.Add(new CellIssue(span.StartLinePosition.Line, span.StartLinePosition.Character, length, key, message));
    }
}
