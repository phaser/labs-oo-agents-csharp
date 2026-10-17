// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Runtime;

/// <summary>
/// Checks the code of a cell before the engine compiles it. The code validator (T16) implements this seam.
/// </summary>
public interface ICellValidator
{
    /// <summary>Checks the code. The code has no markdown fences. Throw <see cref="RestrictedCodeException"/> to refuse it.</summary>
    /// <exception cref="RestrictedCodeException">The code uses a forbidden feature.</exception>
    void Validate(string code);
}
