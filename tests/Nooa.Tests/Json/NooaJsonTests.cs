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

    [Fact]
    public void Options_HaveATypeInfoResolver_SoSchemaBuildingWorksAfterFirstUse()
    {
        _ = JsonSerializer.Serialize(new Sample(1, Nooa.ContextBlocks.ResultStatus.Complete), NooaJson.Options);

        Assert.NotNull(NooaJson.Options.TypeInfoResolver);
        Assert.NotNull(Nooa.Llm.ToolSchema.FromType(typeof(List<string>)));
    }
}
