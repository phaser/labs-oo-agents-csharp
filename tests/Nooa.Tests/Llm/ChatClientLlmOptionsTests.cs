// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class ChatClientLlmOptionsTests
{
    [Fact]
    public void Defaults_SetATokenBudgetAndTimeout()
    {
        var options = new ChatClientLlmOptions();

        Assert.Equal(8192, options.MaxOutputTokens);
        Assert.Equal(TimeSpan.FromSeconds(600), options.RequestTimeout);
        Assert.NotNull(options.Retry);
        Assert.False(options.ExtractThinkTags);
        Assert.True(options.StrictTools);
        options.Validate();
    }

    [Fact]
    public void Validate_NullLimits_AreAllowed()
    {
        new ChatClientLlmOptions { MaxOutputTokens = null, RequestTimeout = null, ContextWindow = null }.Validate();
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, null, 0)]
    public void Validate_NonPositiveValue_Throws(int? maxOutput, int? window, int? timeoutSeconds)
    {
        var options = new ChatClientLlmOptions
        {
            MaxOutputTokens = maxOutput ?? 1,
            ContextWindow = window,
            RequestTimeout = TimeSpan.FromSeconds(timeoutSeconds ?? 1),
        };

        Assert.Throws<ConfigValidationException>(options.Validate);
    }
}
