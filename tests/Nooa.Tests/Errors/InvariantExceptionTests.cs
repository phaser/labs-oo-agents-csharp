// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_strategy_validators.py

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class InvariantExceptionTests
{
    [Fact]
    public void New_IsAValidationException()
    {
        var inner = new InvalidOperationException("x");

        var error = new InvariantException("bad", inner);

        Assert.IsAssignableFrom<ValidationException>(error);
        Assert.Equal("bad", error.Message);
        Assert.Same(inner, error.InnerException);
    }
}
