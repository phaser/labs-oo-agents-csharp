// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/token_counter.py

using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class TokenCounterTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcdefgh", 2)]
    [InlineData("abcdefghi", 2)]
    public void CharApproximate_Text_IsLengthOverFourRoundedDown(string text, int expected) =>
        Assert.Equal(expected, TokenCounter.CharApproximate(text));

    [Fact]
    public void CharApproximate_SurrogatePairs_CountAsOneCharacter() =>
        Assert.Equal(1, TokenCounter.CharApproximate(string.Concat(Enumerable.Repeat("\U0001F600", 4))));
}
