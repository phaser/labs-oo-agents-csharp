// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>The doc text of one parameter. Position -1 is the return value.</summary>
internal sealed record ParamDocModel(int Position, string Text);
