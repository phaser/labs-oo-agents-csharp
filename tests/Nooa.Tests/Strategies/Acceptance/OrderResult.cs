// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart (a copy of the example type for the unit tests; the example has its own in examples/Quickstart)

namespace Nooa.Tests.Strategies.Acceptance;

public sealed record OrderResult(bool CanFulfill, double TotalCost, List<string> UnavailableItems);
