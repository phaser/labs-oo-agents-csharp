// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

public class AgenticAttributeTests
{
    [Fact]
    public void Usage_TargetsMethodsOnly_NotRepeatable()
    {
        var usage = AttributeUsageOf(typeof(AgenticAttribute));

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    internal static AttributeUsageAttribute AttributeUsageOf(Type attribute)
    {
        return (AttributeUsageAttribute)Attribute.GetCustomAttribute(attribute, typeof(AttributeUsageAttribute))!;
    }
}
