// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

[Collection(AgentStaticCollection.Name)]
public class TraceSuppressionTests
{
    [Fact]
    public async Task IsActive_FlowsDownTheAsyncFlowOnly()
    {
        Assert.False(TraceSuppression.IsActive);
        TraceSuppression.IsActive = true;
        try
        {
            Assert.True(await Task.Run(() => TraceSuppression.IsActive));
        }
        finally
        {
            TraceSuppression.IsActive = false;
        }

        Assert.False(await Task.Run(() => TraceSuppression.IsActive));
    }
}
