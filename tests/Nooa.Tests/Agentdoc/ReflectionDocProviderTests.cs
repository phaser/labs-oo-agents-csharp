// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0649 // Fixture fields are only read by reflection.

using System.ComponentModel;
using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class ReflectionDocProviderTests
{
    [Description("A thing.")]
    private sealed class Thing
    {
        [Description("The name.")]
        public string Name = "";

        public int Plain;

        public void Run([Description("How many.")] int count, int other)
        {
        }
    }

    [Fact]
    public void GetSummary_Described_ReturnsText()
    {
        Assert.Equal("A thing.", ReflectionDocProvider.Instance.GetSummary(typeof(Thing)));
        Assert.Equal("The name.", ReflectionDocProvider.Instance.GetSummary(typeof(Thing).GetField("Name")!));
    }

    [Fact]
    public void GetSummary_Undescribed_ReturnsNull()
    {
        Assert.Null(ReflectionDocProvider.Instance.GetSummary(typeof(Thing).GetField("Plain")!));
    }

    [Fact]
    public void GetParameterSummary_Described_ReturnsText()
    {
        var parameters = typeof(Thing).GetMethod("Run")!.GetParameters();
        Assert.Equal("How many.", ReflectionDocProvider.Instance.GetParameterSummary(parameters[0]));
        Assert.Null(ReflectionDocProvider.Instance.GetParameterSummary(parameters[1]));
    }
}
