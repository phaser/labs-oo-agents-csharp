// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

/// <summary>
/// Tests that read or change process environment variables share this collection, so they run one at a time.
/// </summary>
[CollectionDefinition(Name)]
public sealed class EnvironmentCollection
{
    public const string Name = "Process environment";
}
