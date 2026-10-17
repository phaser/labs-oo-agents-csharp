// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Runtime;

/// <summary>One rule that a cell breaks. Line and column start at zero.</summary>
/// <param name="Line">The line of the code.</param>
/// <param name="Column">The column of the first character.</param>
/// <param name="Length">The number of characters to mark. Zero marks one character.</param>
/// <param name="Key">Two issues with the same key on the same line are one issue.</param>
/// <param name="Message">What is wrong and what to do instead.</param>
internal sealed record CellIssue(int Line, int Column, int Length, string Key, string Message);
