// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py

namespace Nooa.Examples.Quickstart.Example05;

/// <summary>The appraisal details of an artwork.</summary>
/// <param name="Title">The title of the work.</param>
/// <param name="Artist">The artist.</param>
/// <param name="Value">The current market value.</param>
/// <param name="Currency">The currency of the value.</param>
public sealed record Appraisal(string Title, string Artist, double Value, string Currency);
