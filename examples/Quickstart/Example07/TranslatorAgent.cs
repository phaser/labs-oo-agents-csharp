// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/07_dynamic_prompts.py

namespace Nooa.Examples.Quickstart.Example07;

/// <summary>Agent that translates text with configurable behavior.</summary>
public partial class TranslatorAgent(string targetLanguage = "Spanish", AgentOptions? options = null) : Agent(options)
{
    /// <summary>The language of the translation. The doc comments below read it.</summary>
    public string TargetLanguage { get; } = targetLanguage;

    /// <summary>
    /// Translate the text to {TargetLanguage}.
    ///
    /// Keep the translation natural and idiomatic.
    /// </summary>
    [Agentic]
    public partial Task<string> TranslateAsync(string text);

    /// <summary>
    /// Translate the text to {TargetLanguage} using formal register.
    ///
    /// Use polite/formal forms (e.g., 'usted' in Spanish, 'Sie' in German).
    /// </summary>
    [Agentic]
    public partial Task<string> TranslateFormalAsync(string text);
}
