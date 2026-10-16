// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

namespace Nooa.Tests;

public class HiddenAttributeTests
{
    private sealed class Sample
    {
        [Hidden]
        public int Secret;

        public int Visible;
    }

    [Fact]
    public void Attribute_OnField_IsFound()
    {
        Assert.True(Attribute.IsDefined(typeof(Sample).GetField(nameof(Sample.Secret))!, typeof(HiddenAttribute)));
        Assert.False(Attribute.IsDefined(typeof(Sample).GetField(nameof(Sample.Visible))!, typeof(HiddenAttribute)));
    }
}
