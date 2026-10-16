// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Strategies;

/// <summary>
/// One model-visible argument of a call: parameter name, declared type and value.
/// </summary>
public sealed record CallArgument(string Name, Type Type, object? Value);
