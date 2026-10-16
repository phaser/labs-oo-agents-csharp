// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tests;

public class AgentOptionsAttributeTests
{
    [Fact]
    public void Llm_IsSetByInitializer_AndDefaultsToNull()
    {
        Assert.Equal("gpt", new AgentOptionsAttribute { Llm = "gpt" }.Llm);
        Assert.Null(new AgentOptionsAttribute().Llm);
    }

    [Fact]
    public void Usage_TargetsClassesOnly()
    {
        Assert.Equal(AttributeTargets.Class, AgenticAttributeTests.AttributeUsageOf(typeof(AgentOptionsAttribute)).ValidOn);
    }
}
