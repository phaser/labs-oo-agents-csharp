// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Nooa.ContextBlocks;
using Nooa.Json;

namespace Nooa.Tests;

/// <summary>Serialize and deserialize through the polymorphic base, as storage does.</summary>
internal static class EventJson
{
    public static string Write(EventBase e) => JsonSerializer.Serialize(e, NooaJson.Options);

    public static EventBase Read(string json) => JsonSerializer.Deserialize<EventBase>(json, NooaJson.Options)!;

    public static T RoundTrip<T>(T e) where T : EventBase => Assert.IsType<T>(Read(Write(e)));
}
