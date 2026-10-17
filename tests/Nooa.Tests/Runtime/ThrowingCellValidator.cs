// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public sealed class ThrowingCellValidator(string forbidden) : ICellValidator
{
    public int Calls { get; private set; }

    public void Validate(string code)
    {
        Calls++;
        if (code.Contains(forbidden, StringComparison.Ordinal))
        {
            throw new RestrictedCodeException($"{forbidden} is forbidden", 1);
        }
    }
}
