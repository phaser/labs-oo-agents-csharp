// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nooa.Generators;

/// <summary>Display formats for emitted type names.</summary>
internal static class SymbolFormats
{
    /// <summary>Fully qualified, with nullable reference annotations. For declarations.</summary>
    public static readonly SymbolDisplayFormat Declared = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Fully qualified, without nullable reference annotations. For <c>typeof</c>.</summary>
    public static readonly SymbolDisplayFormat TypeOf = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>Short names. For text that the model reads.</summary>
    public static readonly SymbolDisplayFormat Short = SymbolDisplayFormat.MinimallyQualifiedFormat;

    /// <summary>The C# name of a parameter, with <c>@</c> when it is a keyword.</summary>
    public static string Identifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }

    /// <summary>True when <c>typeof</c> can name the type and reflection can find it.</summary>
    public static bool IsReflectable(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol:
            case IDynamicTypeSymbol:
            case IPointerTypeSymbol:
            case IFunctionPointerTypeSymbol:
            case IErrorTypeSymbol:
                return false;
            case IArrayTypeSymbol array:
                return IsReflectable(array.ElementType);
            case INamedTypeSymbol named:
                foreach (var argument in named.TypeArguments)
                {
                    if (!IsReflectable(argument))
                    {
                        return false;
                    }
                }

                return true;
            default:
                return true;
        }
    }
}
