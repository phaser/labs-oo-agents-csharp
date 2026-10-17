// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: examples/quickstart/07_dynamic_prompts.py

namespace Nooa.Examples.Quickstart.Example07;

/// <summary>Quickstart 07: {TargetLanguage} in a doc comment is replaced with the value of the property at run time.</summary>
public sealed class DynamicPromptsExample : IQuickstartExample
{
    public int Number => 7;

    public string Title => "Dynamic prompts";

    public async Task RunAsync(ILlmClient llm, TextWriter output)
    {
        var options = new AgentOptions { Llm = llm };
        var spanish = new TranslatorAgent("Spanish", options);
        var german = new TranslatorAgent("German", options);
        const string text = "Hello, how are you today?";

        await output.WriteLineAsync($"Original: {text}");
        await output.WriteLineAsync($"Spanish: {await spanish.TranslateAsync(text)}");
        await output.WriteLineAsync($"German: {await german.TranslateAsync(text)}");
        await output.WriteLineAsync($"Spanish (formal): {await spanish.TranslateFormalAsync(text)}");
    }
}
