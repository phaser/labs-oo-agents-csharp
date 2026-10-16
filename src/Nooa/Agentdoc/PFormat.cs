// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/__init__.py, src/nooa/runtime/pprint.py

using System.Globalization;
using Nooa.Config;

namespace Nooa.Agentdoc;

/// <summary>
/// Renders a value as compact C#-like text for the model: the <c>state</c> block and the REPL output
/// (upstream <c>pformat()</c>, <c>pprint()</c> and <c>truncating_pformat()</c>).
/// </summary>
/// <remarks>
/// <para>
/// Strings are C# literals. A collection is <c>[1, 2]</c>, a tuple <c>(1, 2)</c>, a set <c>{1, 2}</c> and a
/// dictionary <c>{"k": v}</c>. An object is <c>Name { Field = value }</c>. A cut value shows a marker that has the
/// full count, for example <c>List&lt;int&gt;(len=100, [:5]=[0, 1, 2, 3, 4], [-5:]=[95, 96, 97, 98, 99])</c>.
/// A marker always means that the value is not complete.
/// </para>
/// <para>
/// An object shows its public fields and auto-properties only. A member marked <see cref="HiddenAttribute"/>, a member
/// with a leading underscore and a computed property are left out. No property getter runs. A cycle shows <c>&lt;cycle&gt;</c>.
/// A type with its own <c>ToString</c> shows that text.
/// </para>
/// </remarks>
public static class PFormat
{
    /// <summary>Formats a value.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="maxLength">Maximum items per collection and members per object. Null means no limit.</param>
    /// <param name="maxString">Maximum characters per string. Null means no limit.</param>
    /// <param name="maxDepth">Maximum nesting depth. Null means no limit (an internal safety limit still applies).</param>
    /// <param name="expandAll">Always write a collection on many lines.</param>
    /// <param name="unquoteStrings">Return an untruncated top-level string as it is, without quotes.</param>
    public static string Format(
        object? value,
        int? maxLength = null,
        int? maxString = null,
        int? maxDepth = null,
        bool expandAll = false,
        bool unquoteStrings = false)
    {
        if (unquoteStrings && value is string text && (maxString is null || text.Length <= maxString))
        {
            return text;
        }

        var writer = new StringWriter(CultureInfo.InvariantCulture);
        Write(writer, value, maxLength, maxString, maxDepth, expandAll);
        return writer.ToString();
    }

    /// <summary>Formats a value with the limits of a <see cref="FormatConfig"/>.</summary>
    public static string Format(object? value, FormatConfig config, bool expandAll = false, bool unquoteStrings = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Format(value, config.MaxLength, config.MaxString, config.MaxDepth, expandAll, unquoteStrings);
    }

    /// <summary>
    /// Formats a value with a hard cap on the size of the output. A string is returned as it is.
    /// Output over the cap keeps the head and the tail, with a notice for the part that is dropped.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="maxChars">The cap in characters. Null means no cap.</param>
    /// <param name="maxLength">Maximum items per collection and members per object. Null means no limit.</param>
    /// <param name="maxString">Maximum characters per string. Null means no limit.</param>
    /// <param name="maxDepth">Maximum nesting depth. Null means no limit.</param>
    /// <param name="expandAll">Always write a collection on many lines.</param>
    public static string TruncatingFormat(
        object? value,
        int? maxChars = null,
        int? maxLength = null,
        int? maxString = null,
        int? maxDepth = null,
        bool expandAll = false)
    {
        if (maxChars is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChars), maxChars, "maxChars must be > 0 or null.");
        }

        if (value is string text)
        {
            return text;
        }

        using TextWriter writer = maxChars is { } limit ? new TruncatingTextWriter(limit) : new StringWriter(CultureInfo.InvariantCulture);
        Write(writer, value, maxLength, maxString, maxDepth, expandAll);
        return writer.ToString()!;
    }

    /// <summary>Writes a value to <paramref name="writer"/>, with the same rules as <see cref="Format(object?, int?, int?, int?, bool, bool)"/>.</summary>
    /// <param name="writer">The destination. Use a <see cref="TruncatingTextWriter"/> for a hard cap.</param>
    /// <param name="value">The value to format.</param>
    /// <param name="maxLength">Maximum items per collection and members per object. Null means no limit.</param>
    /// <param name="maxString">Maximum characters per string. Null means no limit.</param>
    /// <param name="maxDepth">Maximum nesting depth. Null means no limit.</param>
    /// <param name="expandAll">Always write a collection on many lines.</param>
    public static void Write(
        TextWriter writer,
        object? value,
        int? maxLength = null,
        int? maxString = null,
        int? maxDepth = null,
        bool expandAll = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequirePositive(maxLength, nameof(maxLength));
        RequirePositive(maxString, nameof(maxString));
        RequirePositive(maxDepth, nameof(maxDepth));
        new ValueRenderer(maxLength, maxString, maxDepth, expandAll).Write(writer, value);
    }

    private static void RequirePositive(int? limit, string name)
    {
        if (limit is <= 0)
        {
            throw new ArgumentOutOfRangeException(name, limit, name + " must be > 0 or null.");
        }
    }
}
