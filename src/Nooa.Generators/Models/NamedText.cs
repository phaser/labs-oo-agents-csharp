// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>A name and a text, for example a parameter and its doc text.</summary>
internal sealed record NamedText(string Name, string Text);
