// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/storage.py

namespace Nooa.Errors;

/// <summary>
/// The snapshot ID is not found in storage.
/// </summary>
public class SnapshotNotFoundException : NooaException
{
    public SnapshotNotFoundException(string message)
        : base(message)
    {
    }
}
