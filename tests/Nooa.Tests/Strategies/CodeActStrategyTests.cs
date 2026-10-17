// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/strategies/test_codeact_strategy.py (intent)

using Nooa.Config;
using Nooa.Errors;
using Nooa.Llm;
using Nooa.Strategies;

namespace Nooa.Tests.Strategies;

public class CodeActStrategyTests
{
    [Fact]
    public async Task Agent_ThatIsNotPublic_FailsWithAClearError()
    {
        var agent = new HiddenAgent(new AgentOptions { Llm = new FakeLlmClient(), DefaultStrategy = new CodeActStrategy() });

        var e = await Assert.ThrowsAsync<GenerationException>(() => agent.RunAsync());

        Assert.Contains("needs a public agent type", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_Name_BlockOrderAndStaticKeys()
    {
        var strategy = new CodeActStrategy();

        Assert.Equal("CODEACT", strategy.Name);
        Assert.Equal(["system_prompt", "strategy_prompt", "execution_context", "self"], strategy.GetBlockOrder());
        Assert.Equal(["execution_context", "strategy_prompt"], strategy.GetStaticBlockKeys()!.Order());
        Assert.Equal(["strategy_prompt", "execution_context"], strategy.GetBlockOverrides()!.Keys);
    }

    [Fact]
    public void Constructor_InvalidConfig_Throws()
    {
        var config = new CodeActConfig { LoopGuard = new LoopGuardConfig { RepeatThreshold = 1 } };

        Assert.Throws<ConfigValidationException>(() => new CodeActStrategy(config));
    }
}
