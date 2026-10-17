// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/response_cleanup.py

using System.Text.RegularExpressions;

namespace Nooa.Runtime;

/// <summary>Cleanup of model output before it is processed.</summary>
public static partial class ResponseCleanup
{
    /// <summary>
    /// Removes markdown code fences from code that a model wrote. It handles <c>```csharp</c>, <c>```cs</c>, <c>```</c> and any other
    /// language tag. The opening and the closing fence must both be present.
    /// </summary>
    /// <returns>The cleaned code and the opening fence (for example <c>```csharp</c>), or the original code and null when there was no fence.</returns>
    public static (string Code, string? Fence) StripCodeFences(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var stripped = code.Trim();

        // Multiline form. Text after the tag on the opening line means the code starts on the fence line, so this form does not match.
        if (TryMatch(MultilineFence(), stripped) is { } multiline)
        {
            return (multiline.Groups[2].Value.Trim(), multiline.Groups[1].Value);
        }

        if (TryMatch(InlineFence(), stripped) is { } inline)
        {
            return (inline.Groups[2].Value.Trim(), inline.Groups[1].Value);
        }

        return (code, null);
    }

    // The input is model output. A match timeout counts as no match, so the code stays as it is (fail closed).
    private static Match? TryMatch(Regex regex, string input)
    {
        try
        {
            return regex.Match(input) is { Success: true } match ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^(```[^\s`]*)[ \t]*\n(.*?)\n?```$", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MultilineFence();

    [GeneratedRegex(@"^(```[^\s`]*)\s*(.*?)\s*```$", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineFence();
}
