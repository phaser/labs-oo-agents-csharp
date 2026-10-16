// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/storage.py

namespace Nooa.Errors;

/// <summary>
/// An agent was saved without a storage manager.
/// </summary>
public class StorageNotConfiguredException : NooaException
{
    public StorageNotConfiguredException(string message)
        : base(message)
    {
    }
}
