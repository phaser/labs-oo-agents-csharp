// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CS0649 // Fixture fields are only read by the generated code and reflection.

namespace Nooa.Tests.Generated;

/// <summary>
/// You are a support agent for {Company}. Answer in {Settings.Language}. Use {{braces}} literally.
/// </summary>
[AgentOptions(Llm = "fast-model")]
public partial class SampleAgent : RecordingAgent
{
    /// <summary>The company name.</summary>
    public string Company = "Acme";

    /// <summary>Reply settings.</summary>
    public Reply Settings { get; set; } = new();

    private readonly int _hidden = 1;

    /// <summary>Counts the words in <paramref name="text"/>.</summary>
    /// <remarks>Count only letters.</remarks>
    /// <param name="text">The text to count.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The number of words.</returns>
    [Agentic]
    public partial Task<int> CountWordsAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Greet the user of {Company}.</summary>
    [Agentic]
    public partial Task GreetAsync(string name, int times);

    /// <summary>Counts again.</summary>
    [Agentic]
    [Strategy(typeof(string))]
    internal partial ValueTask<string?> LookupAsync(string? key);

    /// <summary>Counts overloaded.</summary>
    [Agentic]
    public partial ValueTask FireAsync(string @event);

    /// <summary>Plain helper.</summary>
    public int Helper(int value) => value + _hidden;

    public sealed class Reply
    {
        public string Language = "English";
    }
}
