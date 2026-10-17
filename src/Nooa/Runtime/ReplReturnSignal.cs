// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py (return_result)

namespace Nooa.Runtime;

/// <summary>
/// Thrown by <see cref="ReplGlobals.ReturnResult"/> to end a cell at once. The engine catches it.
/// </summary>
/// <remarks>
/// Cell code can catch it with <c>catch (Exception)</c> and go on. The globals record the value before they throw,
/// so the engine still sees the result after the cell ends.
/// </remarks>
internal sealed class ReplReturnSignal : Exception
{
    public ReplReturnSignal()
        : base("ReturnResult was called. The cell ends here.")
    {
    }
}
