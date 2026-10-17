// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/tracing/test_session_processor.py

using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class TraceSessionTests
{
    [Fact]
    public void Scope_SetsTheSessionAndRestoresThePreviousValue()
    {
        using var outer = TraceSession.Scope("outer");
        using (TraceSession.Scope("inner"))
        {
            Assert.Equal("inner", TraceSession.Current);
        }

        Assert.Equal("outer", TraceSession.Current);
    }

    [Fact]
    public async Task Set_FlowsToChildTasksButNotToSiblings()
    {
        using var scope = TraceSession.Scope("parent");
        var child = await Task.Run(() =>
        {
            TraceSession.Set("child");
            return TraceSession.Current;
        });

        Assert.Equal("child", child);
        Assert.Equal("parent", TraceSession.Current);
    }

    [Fact]
    public void Current_WithNoSession_FallsBackToTheProcessDefault()
    {
        using var scope = TraceSession.Scope(null);
        TraceSession.SetDefault("default-session");
        try
        {
            Assert.Equal("default-session", TraceSession.Current);
        }
        finally
        {
            TraceSession.SetDefault(null);
        }

        Assert.Null(TraceSession.Current);
    }

    [Fact]
    public void NewId_IsAFileSafeUniqueName()
    {
        var first = TraceSession.NewId();

        Assert.Matches("^[0-9]{8}_[0-9]{6}_[0-9a-f]{8}$", first);
        Assert.NotEqual(first, TraceSession.NewId());
    }
}
