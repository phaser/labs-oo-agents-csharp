// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class AgentDocProvidersTests
{
    private sealed class Registered
    {
        public int Value;
    }

    private sealed class FallsBack
    {
    }

    [Fact]
    public void Default_RegisteredProvider_Wins()
    {
        var fake = new FakeDocProvider();
        fake.Summaries[typeof(Registered)] = "From fake.";
        using (AgentDocProviders.Register(fake))
        {
            Assert.Equal("From fake.", AgentDocProviders.Default.GetSummary(typeof(Registered)));
        }
    }

    [Fact]
    public void Default_AfterDispose_ProviderIsRemoved()
    {
        var fake = new FakeDocProvider();
        fake.Summaries[typeof(FallsBack)] = "Temporary.";
        AgentDocProviders.Register(fake).Dispose();
        Assert.Null(AgentDocProviders.Default.GetSummary(typeof(FallsBack)));
    }

    [Fact]
    public void Register_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentDocProviders.Register(null!));
    }
}
