// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using Nooa.Generators.Models;

namespace Nooa.Generators;

/// <summary>
/// Parses a doc comment prompt. <c>{Member.Path}</c> is a placeholder. <c>{{</c> and <c>}}</c> are literal braces.
/// </summary>
internal static class PromptParser
{
    /// <summary>
    /// Returns the prompt, or null and an error message. <paramref name="resolve"/> maps a member path to a C#
    /// expression on the variable <c>a</c>, or to null when the path is not allowed.
    /// </summary>
    public static PromptModel? Parse(string text, Func<string, string?> resolve, out string? error)
    {
        error = null;
        var literal = new StringBuilder();
        var format = new StringBuilder();
        var arguments = new List<string>();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '{' && next == '{')
            {
                literal.Append('{');
                format.Append("{{");
                i++;
            }
            else if (c == '}' && next == '}')
            {
                literal.Append('}');
                format.Append("}}");
                i++;
            }
            else if (c == '{')
            {
                var end = text.IndexOf('}', i + 1);
                if (end < 0)
                {
                    error = "The placeholder at position " + i + " has no closing '}'. Write '{{' for a literal '{'";
                    return null;
                }

                var path = text.Substring(i + 1, end - i - 1);
                var expression = IsPath(path) ? resolve(path) : null;
                if (expression is null)
                {
                    error = "Placeholder '{" + path + "}' is not a path of readable instance fields or properties of this agent. Write '{{' for a literal '{'";
                    return null;
                }

                format.Append('{').Append(arguments.Count).Append('}');
                arguments.Add(expression);
                i = end;
            }
            else if (c == '}')
            {
                error = "Unmatched '}' at position " + i + ". Write '}}' for a literal '}'";
                return null;
            }
            else
            {
                literal.Append(c);
                format.Append(c);
            }
        }

        return arguments.Count == 0
            ? new PromptModel(literal.ToString(), EquatableArray<string>.Empty)
            : new PromptModel(format.ToString(), new EquatableArray<string>(arguments));
    }

    private static bool IsPath(string path)
    {
        var expectStart = true;
        foreach (var c in path)
        {
            if (expectStart)
            {
                if (!(char.IsLetter(c) || c == '_'))
                {
                    return false;
                }

                expectStart = false;
            }
            else if (c == '.')
            {
                expectStart = true;
            }
            else if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return !expectStart;
    }
}
