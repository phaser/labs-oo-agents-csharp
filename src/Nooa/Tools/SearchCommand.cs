// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_tools.py

using System.Text.RegularExpressions;

namespace Nooa.Tools;

/// <summary>
/// Recognizes a pure search command (a bare grep, egrep or rg) whose matches map one to one to lines. The check is conservative:
/// anything that can make grep and ripgrep disagree, or that reshapes the output, is not a pure search. A wrong answer can only
/// lose an anchor. It cannot make a wrong one.
/// </summary>
public static partial class SearchCommand
{
    private const int _regexTimeoutMs = 1000;

    // Short flags that drop the line anchor or change match semantics:
    // o only-matching, c count, l/L files, A/B/C context, z/Z NUL data, P PCRE.
    private const string _anchorBreakingFlags = "oclLABCDzZP";

    private static readonly string[] _longAnchorBreaking =
    [
        "--pcre2",
        "--null-data",
        "--count",
        "--files-with-matches",
        "--files-without-match",
        "--only-matching",
        "--multiline",
        "--context",
        "--after-context",
        "--before-context",
    ];

    /// <summary>True when <paramref name="command"/> is a bare grep, egrep or rg whose matches map one to one to lines.</summary>
    /// <remarks>Also accepts <c>find ... | xargs grep -n ...</c>, a trailing <c>| head</c> or <c>| tail</c>, and a leading <c>cd ... &amp;&amp;</c>.</remarks>
    public static bool IsPure(string command)
    {
        // The command is model-written. A regex timeout means "not pure": fail closed, never throw.
        try
        {
            return IsPureCore(command);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool IsPureCore(string command)
    {
        var c = command.Trim();
        if (c.Contains("&&", StringComparison.Ordinal))
        {
            c = c[(c.LastIndexOf("&&", StringComparison.Ordinal) + 2)..].Trim();
        }

        var withoutTail = SafeTailPipe().Replace(c, "").Trim();
        if (XargsGrep().IsMatch(withoutTail))
        {
            var grepPart = "grep" + withoutTail[(withoutTail.IndexOf("xargs grep", StringComparison.Ordinal) + "xargs grep".Length)..];
            grepPart = SafeTailPipe().Replace(grepPart, "").Trim();
            var clusters = ShortClusters(grepPart);
            return clusters.All(cluster => !cluster.Any(_anchorBreakingFlags.Contains)) && clusters.Any(cluster => cluster.Contains('n'));
        }

        if (!SearchHead().IsMatch(withoutTail) || ManglingPipe().IsMatch(withoutTail) || HasUnquotedPipe(withoutTail))
        {
            return false;
        }

        if (_longAnchorBreaking.Any(flag => withoutTail.Contains(flag, StringComparison.Ordinal)))
        {
            return false;
        }

        return ShortClusters(withoutTail).All(cluster => !cluster.Any(_anchorBreakingFlags.Contains));
    }

    /// <summary>Removes a leading <c>cd ... &amp;&amp;</c>, a safe tail pipe, and the <c>find ... | xargs</c> part, so only the search remains.</summary>
    internal static string ReduceToSearch(string command)
    {
        var cmd = command.Trim();
        if (cmd.Contains("&&", StringComparison.Ordinal))
        {
            cmd = cmd[(cmd.LastIndexOf("&&", StringComparison.Ordinal) + 2)..].Trim();
        }

        cmd = SafeTailPipe().Replace(cmd, "").Trim();
        if (XargsGrep().IsMatch(cmd))
        {
            cmd = "grep" + cmd[(cmd.IndexOf("xargs grep", StringComparison.Ordinal) + "xargs grep".Length)..];
        }

        return cmd;
    }

    /// <summary>True when the command ends in a pipe that only cuts the output (<c>| head</c> or <c>| tail</c>).</summary>
    internal static bool EndsInTailPipe(string command) => SafeTailPipe().IsMatch(command.Trim());

    /// <summary>
    /// Extracts the pattern, the paths and two flags from a grep or rg command. Returns null when the structure is not understood.
    /// </summary>
    internal static ParsedSearch? Parse(string command)
    {
        List<string> tokens;
        try
        {
            tokens = ShellWords.Split(command);
        }
        catch (FormatException)
        {
            return null;
        }

        if (tokens.Count == 0 || tokens[0] is not ("grep" or "egrep" or "rg"))
        {
            return null;
        }

        var ignoreCase = false;
        var fixedStrings = false;
        var positional = new List<string>();
        for (var i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token == "--")
            {
                positional.AddRange(tokens.Skip(i + 1));
                break;
            }

            if (token.Length > 1 && token[0] == '-')
            {
                if (token == "--ignore-case")
                {
                    ignoreCase = true;
                }
                else if (token == "--fixed-strings")
                {
                    fixedStrings = true;
                }
                else if (token.StartsWith("--", StringComparison.Ordinal))
                {
                    return null; // An unknown long flag can take a value. Stop to be safe.
                }
                else
                {
                    foreach (var letter in token.AsSpan(1))
                    {
                        switch (letter)
                        {
                            case 'i':
                                ignoreCase = true;
                                break;
                            case 'F':
                                fixedStrings = true;
                                break;
                            case 'r' or 'R' or 'n' or 'H':
                                break; // Recursive, line number, with filename: harmless.
                            default:
                                return null;
                        }
                    }
                }

                continue;
            }

            positional.Add(token);
        }

        return positional.Count == 0 ? null : new ParsedSearch(positional[0], positional.Skip(1).ToList(), ignoreCase, fixedStrings);
    }

    private static List<string> ShortClusters(string command)
        => ShortFlagCluster().Matches(command).Select(m => m.Groups[1].Value).ToList();

    private static bool HasUnquotedPipe(string command)
    {
        var inSingle = false;
        var inDouble = false;
        foreach (var c in command)
        {
            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }
            else if (c == '|' && !inSingle && !inDouble)
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\|\s*(sed|awk|cut|sort|uniq|tr|head|tail|wc|xargs|rev)\b", RegexOptions.NonBacktracking, _regexTimeoutMs)]
    private static partial Regex ManglingPipe();

    [GeneratedRegex(@"^\s*(grep|egrep|rg)\b", RegexOptions.NonBacktracking, _regexTimeoutMs)]
    private static partial Regex SearchHead();

    [GeneratedRegex(@"\|\s*(head|tail)(\s+-[0-9n]+)?\s*$", RegexOptions.NonBacktracking, _regexTimeoutMs)]
    private static partial Regex SafeTailPipe();

    [GeneratedRegex(@"find\s+.+\|\s*xargs\s+grep", RegexOptions.NonBacktracking, _regexTimeoutMs)]
    private static partial Regex XargsGrep();

    [GeneratedRegex(@"(?:^|\s)-([A-Za-z]+)", RegexOptions.NonBacktracking, _regexTimeoutMs)]
    private static partial Regex ShortFlagCluster();

    /// <summary>The parts of a search command that the anchor harvest needs.</summary>
    internal sealed record ParsedSearch(string Pattern, IReadOnlyList<string> Paths, bool IgnoreCase, bool FixedStrings);
}
