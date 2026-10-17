// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests.Runtime;

/// <summary>User code that a cell calls and that throws.</summary>
public static class ReplHelper
{
    public static void Boom() => throw new InvalidOperationException("helper failed");
}
