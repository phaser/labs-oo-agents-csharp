// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>
/// A prompt text. Without placeholders, <see cref="Value"/> is the final text and <see cref="Arguments"/> is empty.
/// With placeholders, <see cref="Value"/> is a <c>string.Format</c> format and <see cref="Arguments"/> holds one
/// C# expression per hole. Each expression reads a member path from the variable <c>a</c>.
/// </summary>
internal sealed record PromptModel(string Value, EquatableArray<string> Arguments)
{
    public bool HasPlaceholders => Arguments.Count > 0;
}
