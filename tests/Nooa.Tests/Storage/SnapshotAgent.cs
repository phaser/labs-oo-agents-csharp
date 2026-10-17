// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0649 // Fixture fields are read and written by reflection.
#pragma warning disable CA1051 // Fixture fields are public on purpose.

using System.Text.Json.Serialization;
using Nooa.Llm;

namespace Nooa.Tests.Storage;

/// <summary>Agent fixture with state of every kind that a snapshot must handle.</summary>
public class SnapshotAgent(AgentOptions? options = null) : Agent(options)
{
    public enum Color
    {
        Red,
        Green,
    }

    public int Counter;

    public string Name = "";

    public double Ratio;

    public Color Favorite;

    public int? Maybe;

    public DateTimeOffset Stamp;

    public List<string> Notes = [];

    public Dictionary<string, int> Scores = [];

    public int[] Numbers = [];

    public Profile? Owner;

    public Shape? Figure;

    public PlainBag? Bag;

    public Legacy? Old;

    public object? Open;

    public ILlmClient? Client;

    [NoSnapshot]
    public int Scratch;

    public int Level { get; set; }

    [NoSnapshot]
    public int Skipped { get; set; }

    public NoSnapshotHolder? Holder;

    public int ReadPrivate() => _secret;

    public void WritePrivate(int value) => _secret = value;

    private int _secret;

    public record Profile(string Email, List<string> Tags);

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(Circle), "circle")]
    public abstract record Shape;

    public sealed record Circle(double Radius) : Shape;

    [Snapshotable]
    public sealed class PlainBag
    {
        public string Label { get; set; } = "";
    }

    public sealed class Legacy
    {
        public string Value { get; set; } = "";
    }

    [NoSnapshot]
    public sealed class NoSnapshotHolder
    {
        public int X { get; set; }
    }
}
