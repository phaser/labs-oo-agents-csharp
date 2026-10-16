// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/events.py

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nooa.Events;

/// <summary>An inclusive range of event sequence numbers. It is persisted as the array <c>[start, end]</c>.</summary>
[JsonConverter(typeof(SeqRangeConverter))]
public readonly record struct SeqRange(int Start, int End)
{
    private sealed class SeqRangeConverter : JsonConverter<SeqRange>
    {
        public override SeqRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("A sequence range must be a JSON array of two integers");
            }

            var values = new int[2];
            for (var i = 0; i < 2; i++)
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
                {
                    throw new JsonException("A sequence range must be a JSON array of two integers");
                }

                values[i] = reader.GetInt32();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("A sequence range must be a JSON array of two integers");
            }

            return new SeqRange(values[0], values[1]);
        }

        public override void Write(Utf8JsonWriter writer, SeqRange value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.Start);
            writer.WriteNumberValue(value.End);
            writer.WriteEndArray();
        }
    }
}
