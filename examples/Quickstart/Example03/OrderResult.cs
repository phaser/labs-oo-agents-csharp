// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/03_codeact_tools.py

namespace Nooa.Examples.Quickstart.Example03;

public sealed record OrderResult(bool CanFulfill, double TotalCost, List<string> UnavailableItems);
