// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/test_event_backend_protocol.py

using Nooa.ContextBlocks;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class InMemoryEventBackendTests : IEventBackendTests
{
    private readonly InMemoryEventBackend _backend = new();

    protected override IEventBackend Backend => _backend;

    [Fact]
    public void Update_MutatesTheLiveEvent()
    {
        var e = new UserEvent { Content = "live" };
        _backend.Store("1", e);

        _backend.SetStatus("1", EventStatus.Archived);

        Assert.Same(e, _backend.Get("1"));
        Assert.Equal(EventStatus.Archived, e.Status);
    }

    [Fact]
    public void MaxTagNum_UsesRangeEnds()
    {
        _backend.Store("12..234", new UserEvent { Content = "a" });
        _backend.Store("500", new UserEvent { Content = "b" });
        _backend.Store("233..293432", new UserEvent { Content = "c" });

        Assert.Equal(293432, _backend.MaxTagNum());
    }

    [Fact]
    public void MaxTagNum_Empty_IsZero() => Assert.Equal(0, _backend.MaxTagNum());

    [Fact]
    public void FindTag_MatchesByIdWhenInstanceDiffers()
    {
        var e = new UserEvent { Content = "a" };
        _backend.Store("7", e);

        Assert.Equal("7", _backend.FindTag(e with { }));
    }
}
