// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nooa.Json;

/// <summary>
/// Serializer options for persisted NOOA values. Property names and enum values are snake_case, which matches
/// the upstream wire format. Always serialize and deserialize events through <see cref="Nooa.ContextBlocks.EventBase"/>
/// so the <c>event_type</c> discriminator is written and read.
/// </summary>
public static class NooaJson
{
    /// <summary>The shared options. Do not change them after first use.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}
