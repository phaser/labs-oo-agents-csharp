// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Strategies;

public sealed record PredictPerson
{
    public required string Name { get; init; }

    public required int Age { get; init; }
}
