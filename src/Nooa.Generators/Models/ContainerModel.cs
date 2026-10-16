// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Generators.Models;

/// <summary>A type that contains the agent class: its keyword (<c>class</c> or <c>record</c>) and name.</summary>
internal sealed record ContainerModel(string Keyword, string Name);
