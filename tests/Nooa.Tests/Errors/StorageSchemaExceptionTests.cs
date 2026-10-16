// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Tests.Errors;

public class StorageSchemaExceptionTests
{
    [Fact]
    public void Constructor_KeepsMessage() => Assert.Equal("bad", new StorageSchemaException("bad").Message);

    [Fact]
    public void Type_DerivesFromBase() => Assert.True(typeof(NooaException).IsAssignableFrom(typeof(StorageSchemaException)));
}
