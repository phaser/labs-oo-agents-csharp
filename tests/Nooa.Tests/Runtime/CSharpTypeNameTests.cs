// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class CSharpTypeNameTests
{
    public sealed class Outer<T>
    {
        public sealed class Inner<U>;
    }

    [Theory]
    [InlineData(typeof(int), "global::System.Int32")]
    [InlineData(typeof(int?), "global::System.Int32?")]
    [InlineData(typeof(string[,]), "global::System.String[,]")]
    [InlineData(typeof(List<string>), "global::System.Collections.Generic.List<global::System.String>")]
    [InlineData(typeof(Dictionary<string, int[]>), "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Int32[]>")]
    [InlineData(typeof(Outer<int>.Inner<bool>), "global::Nooa.Tests.Runtime.CSharpTypeNameTests.Outer<global::System.Int32>.Inner<global::System.Boolean>")]
    public void Of_Type_WritesCompilableName(Type type, string expected)
    {
        Assert.Equal(expected, CSharpTypeName.Of(type));
    }

    [Fact]
    public void Of_PointerOrByRef_Throws()
    {
        Assert.Throws<ArgumentException>(() => CSharpTypeName.Of(typeof(int).MakeByRefType()));
        Assert.Throws<ArgumentException>(() => CSharpTypeName.Of(typeof(int*)));
    }
}
