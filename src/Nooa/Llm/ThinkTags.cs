// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/unifiedllm.py

using System.Text.RegularExpressions;

namespace Nooa.Llm;

/// <summary>Separates the reasoning that open models write inside <c>&lt;think&gt;</c> tags.</summary>
public static partial class ThinkTags
{
    private const int _regexTimeoutMs = 1000;

    /// <summary>
    /// Splits <paramref name="content"/> into the answer and the reasoning. A complete
    /// <c>&lt;think&gt;...&lt;/think&gt;</c> block is removed. If only the closing tag is present (some servers
    /// strip the opening tag), the text before it is the reasoning. The reasoning is null when no tag is found.
    /// </summary>
    public static (string Content, string? Reasoning) Extract(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            var match = ThinkBlock().Match(content);
            if (match.Success)
            {
                return (ThinkBlock().Replace(content, "").Trim(), match.Groups[1].Value.Trim());
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Model output that makes the scan too slow is left as it is.
            return (content, null);
        }

        var close = content.IndexOf("</think>", StringComparison.Ordinal);
        if (close >= 0)
        {
            return (content[(close + "</think>".Length)..].Trim(), content[..close].Trim());
        }

        return (content, null);
    }

    [GeneratedRegex("<think>(.*?)</think>", RegexOptions.Singleline, _regexTimeoutMs)]
    private static partial Regex ThinkBlock();
}
