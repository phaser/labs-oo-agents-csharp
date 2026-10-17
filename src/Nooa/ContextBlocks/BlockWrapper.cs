// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/formatter.py

using System.Globalization;
using System.Net;
using System.Text;

namespace Nooa.ContextBlocks;

/// <summary>
/// Wraps block content in the delimiters of a format (XML tags or Markdown headers).
/// </summary>
/// <remarks>
/// <para>
/// The content of a block can carry text that an attacker controls: a method argument, tool output or an event
/// field. The XML wrapper therefore neutralizes every closing tag in the content: <c>&lt;/</c> becomes <c>&amp;lt;/</c>.
/// Text can never end its own block or its envelope, and it cannot close a sibling block or the system prompt.
/// An opening tag inside a block stays inside the block, as plain text.
/// </para>
/// <para>
/// A block key is not trusted either: code that the model writes can set a key. The wrapper reduces the key to a safe name.
/// A Markdown block has no closing delimiter, so it cannot protect its content. Use the XML formatters for untrusted data.
/// </para>
/// </remarks>
internal static class BlockWrapper
{
    /// <summary>The tag of the envelope around the dynamic blocks in the cached layout.</summary>
    public const string ContextEnvelopeTag = "context";

    /// <summary>The tag around the events that the framework produced for the model.</summary>
    private const string _userEventTag = "sys";

    public static string XmlSystemBlock(ResolvedBlock block)
    {
        var tag = SafeName(block.Key);
        return $"<{tag}>\n{EscapeClosingTags(block.Content)}\n</{tag}>";
    }

    public static string XmlMessageContent(ResolvedBlock block)
    {
        if (block.Role == Role.Assistant)
        {
            return block.Content;
        }

        var label = block.Role == Role.User ? _userEventTag : RoleName(block.Role) + "_message";
        var attributes = block.Metadata.Tag is { Length: > 0 } tag ? $" tag=\"{EscapeAttribute(tag)}\"" : "";
        return $"<{label}{attributes}>\n{EscapeClosingTags(block.Content)}\n</{label}>";
    }

    public static string MarkdownSystemBlock(ResolvedBlock block)
    {
        var header = Title(SafeName(block.Key).Replace('_', ' '));
        return $"# {header}\n\n{block.Content}";
    }

    public static string MarkdownMessageContent(ResolvedBlock block)
    {
        var label = Title(RoleName(block.Role).Replace('_', ' ')) + " Message";
        var meta = block.Metadata.Tag is { Length: > 0 } tag ? $" `{{\"tag\": \"{SafeToken(tag)}\"}}`" : "";
        return $"### {label}{meta}\n\n{block.Content}";
    }

    /// <summary>The provider role name of a role: <c>system</c>, <c>user</c>, <c>assistant</c>, <c>tool</c>, <c>runtime_event</c> or <c>metadata</c>.</summary>
    public static string RoleName(Role role) => role switch
    {
        Role.RuntimeEvent => "runtime_event",
        _ => role.ToString().ToLowerInvariant(),
    };

    /// <summary>Neutralizes every closing tag in <paramref name="content"/>: <c>&lt;/</c> becomes <c>&amp;lt;/</c>.</summary>
    public static string EscapeClosingTags(string content) => content.Replace("</", "&lt;/", StringComparison.Ordinal);

    /// <summary>Escapes a value for use inside a double-quoted XML attribute.</summary>
    public static string EscapeAttribute(string value) => WebUtility.HtmlEncode(value);

    /// <summary>
    /// Reduces a key to a name that is safe as an XML tag and as a one-line header: letters, digits, <c>_</c>,
    /// <c>-</c> and <c>.</c>, and a letter or <c>_</c> first.
    /// </summary>
    public static string SafeName(string key)
    {
        var token = SafeToken(key);
        return token.Length == 0 || !(char.IsAsciiLetter(token[0]) || token[0] == '_') ? "_" + token : token;
    }

    /// <summary>Replaces every character other than letters, digits, <c>_</c>, <c>-</c> and <c>.</c> with <c>_</c>.</summary>
    private static string SafeToken(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_');
        }

        return builder.ToString();
    }

    private static string Title(string text) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
}
