// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_classmethod_rendering.py, test_function_default_rendering.py

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class DocFormatterTests
{
    private enum Mode
    {
        Fast,
        Slow,
    }

    private sealed record Point(int X, int Y);

    private static class Helpers
    {
        public static void Ping()
        {
        }
    }

    private abstract class Base
    {
    }

    private sealed class Box<T>
    {
        public T? Value;

        public TOut Map<TOut>(Func<T, TOut> map, Mode mode = Mode.Slow, params int[] extra) => throw new NotSupportedException();

        public bool TryGet(out int result, ref string text, in double factor)
        {
            result = 0;
            return false;
        }
    }

    private struct Pair
    {
        public int A;
    }

    private static readonly DocFormatter _formatter = new(ReflectionDocProvider.Instance, concise: false);

    [Fact]
    public void FormatType_Kinds_UseMatchingKeyword()
    {
        Assert.StartsWith("record Point\n", _formatter.FormatType(typeof(Point)));
        Assert.StartsWith("static class Helpers\n", _formatter.FormatType(typeof(Helpers)));
        Assert.StartsWith("abstract class Base\n", _formatter.FormatType(typeof(Base)));
        Assert.StartsWith("struct Pair\n", _formatter.FormatType(typeof(Pair)));
    }

    [Fact]
    public void FormatType_GenericType_ShowsParametersAndSignatures()
    {
        var result = _formatter.FormatType(typeof(Box<>));
        Assert.StartsWith("class Box<T>\n", result);
        Assert.Contains("    T Value;", result);
        Assert.Contains("    TOut Map<TOut>(Func<T, TOut> map, Mode mode = Mode.Slow, params int[] extra);", result);
        Assert.Contains("    bool TryGet(out int result, ref string text, in double factor);", result);
    }

    [Fact]
    public void FormatType_Record_ShowsPropertiesWithInitAccessors()
    {
        var result = _formatter.FormatType(typeof(Point), new Point(1, 2));
        Assert.Contains("    int X { get; init; } = 1;", result);
        Assert.Contains("    int Y { get; init; } = 2;", result);
    }

    [Fact]
    public void FormatMethod_Static_HasStaticPrefix()
    {
        var result = _formatter.FormatMethod(typeof(Helpers).GetMethod("Ping")!);
        Assert.Equal("static void Helpers.Ping();", result);
    }
}
