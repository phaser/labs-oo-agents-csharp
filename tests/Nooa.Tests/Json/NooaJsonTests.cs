// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Nooa.Json;

namespace Nooa.Tests.Json;

public class NooaJsonTests
{
    private sealed record Sample(int InputTokens, Nooa.ContextBlocks.ResultStatus Status);

    [Fact]
    public void Options_UseSnakeCaseNamesAndEnumValues()
    {
        var json = JsonSerializer.Serialize(new Sample(1, Nooa.ContextBlocks.ResultStatus.Cancelled), NooaJson.Options);

        Assert.Equal("""{"input_tokens":1,"status":"cancelled"}""", json);
    }
}
