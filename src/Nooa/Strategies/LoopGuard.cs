// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/loop_guard.py

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.Config;

namespace Nooa.Strategies;

/// <summary>
/// Deterministic detection of a CodeAct loop that repeats one tool call. The guard compares each tool call with the most
/// recent ones in a fixed window. It never interprets an error or an output. It only notices that the same action produced
/// the same outcome again, warns once with a fixed template, and stops the generation if the model repeats the action after
/// that warning. One guard serves one generation session. It is not thread-safe.
/// </summary>
internal sealed class LoopGuard
{
    private const int _fingerprintChars = 16;

    private readonly Queue<Entry> _history = new();
    private readonly Dictionary<string, int> _warnedFailing = [];
    private readonly Dictionary<(string Action, string Outcome), int> _warnedRepeats = [];
    private int _calls;

    public LoopGuard(LoopGuardConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Config = config;
    }

    public LoopGuardConfig Config { get; }

    /// <summary>
    /// Hashes the name of a tool call and its normalized arguments. For a code cell the parsed code is used, so comments and
    /// formatting do not change the hash.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="arguments">The parsed arguments, or null when they are not valid JSON.</param>
    /// <param name="rawArguments">The raw argument text. It stands in for <paramref name="arguments"/> when they are not valid JSON.</param>
    /// <param name="codeTool">The name of the code cell tool.</param>
    public static string Fingerprint(string toolName, JsonNode? arguments, string? rawArguments, string codeTool)
    {
        string normalized;
        if (toolName == codeTool && arguments is JsonObject obj)
        {
            var code = obj["code"] is JsonValue v && v.TryGetValue<string>(out var text) ? NormalizeCode(text) : Stable(obj["code"]);
            var rest = new JsonObject(obj.Where(p => p.Key != "code").Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            normalized = Stable(new JsonArray(code, Stable(rest)));
        }
        else
        {
            normalized = arguments is null ? rawArguments ?? "" : Stable(arguments);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{toolName}\0{normalized}"));
        return Convert.ToHexStringLower(hash)[.._fingerprintChars];
    }

    /// <summary>The canonical form of C# source: the syntax tree without comments, written with normalized whitespace.</summary>
    internal static string NormalizeCode(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));
        if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return string.Join('\n', code.Trim().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
        }

        var root = tree.GetRoot();
        var comments = root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
            || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        return root.ReplaceTrivia(comments, (_, _) => default).NormalizeWhitespace().ToFullString();
    }

    /// <summary>
    /// Checks a call before it runs. Returns <see cref="LoopGuardAction.Stop"/> when a blocked failing action is proposed again
    /// soon after its warning, <see cref="LoopGuardAction.Block"/> when its last <c>RepeatThreshold - 1</c> runs in the window
    /// failed identically, and null otherwise.
    /// </summary>
    public LoopGuardVerdict? Before(string action)
    {
        _calls++;
        var matches = _history.Where(e => e.Action == action).ToList();
        if (Recent(_warnedFailing.TryGetValue(action, out var at) ? at : null))
        {
            return new LoopGuardVerdict(LoopGuardAction.Stop, matches.Count + 1, matches.Count > 0 ? matches[^1].Outcome : "");
        }

        _warnedFailing.Remove(action);
        var needed = Config.RepeatThreshold - 1;
        var tail = matches.Skip(Math.Max(0, matches.Count - needed)).ToList();
        if (tail.Count == needed && tail.All(e => e.Failed) && tail.Select(e => e.Outcome).Distinct().Count() == 1)
        {
            _warnedFailing[action] = _calls;
            Append(new Entry(action, tail[^1].Outcome, true));
            return new LoopGuardVerdict(LoopGuardAction.Block, matches.Count + 1, tail[^1].Outcome);
        }

        return null;
    }

    /// <summary>Records the outcome of a call. Warns on, or stops at, identical successful repeats.</summary>
    public LoopGuardVerdict? After(string action, string outcome, bool failed)
    {
        Append(new Entry(action, outcome, failed));
        if (failed)
        {
            return null;
        }

        var key = (action, outcome);
        var repeats = _history.Count(e => e.Action == action && e.Outcome == outcome && !e.Failed);
        if (Recent(_warnedRepeats.TryGetValue(key, out var at) ? at : null))
        {
            return new LoopGuardVerdict(LoopGuardAction.Stop, repeats);
        }

        _warnedRepeats.Remove(key);
        if (repeats >= Config.RepeatThreshold)
        {
            _warnedRepeats[key] = _calls;
            return new LoopGuardVerdict(LoopGuardAction.Nudge, repeats);
        }

        return null;
    }

    /// <summary>Renders the fixed-template message for a verdict.</summary>
    /// <param name="verdict">The verdict.</param>
    /// <param name="toolName">The tool name of the call. It is model text: the caller bounds it.</param>
    /// <param name="completion">True when the code of the call mentions <c>ReturnResult</c>.</param>
    public string Message(LoopGuardVerdict verdict, string toolName, bool completion)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        var window = Config.Window;
        var limit = Config.MaxOutcomeChars;
        var shown = verdict.Outcome.Length <= limit ? verdict.Outcome : verdict.Outcome[..limit] + "...";
        if (verdict.Action == LoopGuardAction.Stop)
        {
            return $"Loop guard stopped generation: the {toolName} call warned about earlier "
                + $"was repeated ({verdict.Repeats} identical calls in the last {window} tool calls).";
        }

        var text = verdict.Action == LoopGuardAction.Block
            ? $"Loop guard: this {toolName} call was not executed. The identical call "
                + $"already failed {verdict.Repeats - 1} times in the last {window} tool calls with "
                + $"the same result:\n{shown}\n"
                + "Do not submit it again; repeating it stops the run. "
                + "Submit a materially different call."
            : $"Loop guard: this {toolName} call ran {verdict.Repeats} times in the last {window} "
                + "tool calls with identical output, so repeating it gives no new information. "
                + "Repeating it again with the same output stops the run. Submit a materially "
                + "different call; if you are waiting for something to change, wait inside "
                + "the cell before checking again.";
        return completion
            ? text + "\nTo finish, build the return value in a variable first, then call ReturnResult(variable)."
            : text;
    }

    /// <summary>Whether the code of a cell call mentions <c>ReturnResult</c>.</summary>
    public static bool MentionsReturnResult(JsonNode? arguments) =>
        arguments is JsonObject obj && obj["code"] is JsonValue v && v.TryGetValue<string>(out var code)
        && code.Contains("ReturnResult", StringComparison.Ordinal);

    private static string Stable(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject obj => "{" + string.Join(",", obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{JsonValue.Create(p.Key)!.ToJsonString()}:{Stable(p.Value)}")) + "}",
        JsonArray array => "[" + string.Join(",", array.Select(Stable)) + "]",
        _ => node.ToJsonString(),
    };

    private bool Recent(int? warnedAt) => warnedAt is { } at && _calls - at <= Config.Window;

    private void Append(Entry entry)
    {
        _history.Enqueue(entry);
        while (_history.Count > Config.Window)
        {
            _history.Dequeue();
        }
    }

    private sealed record Entry(string Action, string Outcome, bool Failed);
}
