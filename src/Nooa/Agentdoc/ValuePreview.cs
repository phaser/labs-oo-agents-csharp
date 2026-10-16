// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace Nooa.Agentdoc;

/// <summary>
/// Placeholder value printer for <c>Doc</c> (field values and parameter defaults). T06 replaces it with the
/// reflection-based <c>PFormat</c> printer and its truncation rules. Keep this the only call site of that printer.
/// </summary>
internal static class ValuePreview
{
    public static string Format(object? value)
    {
        return value switch
        {
            null => "null",
            string s => "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal) + "\"",
            char c => "'" + c + "'",
            bool b => b ? "true" : "false",
            Enum e => e.GetType().Name + "." + e,
            Type t => "typeof(" + TypeNameFormatter.Format(t) + ")",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null",
        };
    }
}
