// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>The text of one XML doc comment. <see cref="Text"/> is the summary followed by the remarks.</summary>
internal sealed record DocComment(string? Text, EquatableArray<NamedText> Params, string? Returns);
