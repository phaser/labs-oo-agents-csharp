// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

// Usage: dotnet run --project examples/Quickstart -- <1-8 | all>
// The examples call a real model and cost money. Set NVIDIA_API_KEY, OPENAI_API_KEY or NVIDIA_INFERENCE_API_KEY first.

using Nooa.Examples.Quickstart;

var examples = QuickstartExamples.CreateAll();
var choice = args.Length == 1 ? args[0] : "";
var selected = choice == "all"
    ? examples
    : [.. examples.Where(e => e.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) == choice)];
if (selected.Count == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --project examples/Quickstart -- <1-8 | all>");
    foreach (var example in examples)
    {
        Console.Error.WriteLine($"  {example.Number}  {example.Title}");
    }

    return 2;
}

Secrets.LoadSecretsIntoEnv();
var (model, llm) = Nooa.Util.Quickstart.CreateDefaultLlm();
Console.WriteLine($"Model: {model}");
foreach (var example in selected)
{
    Console.WriteLine($"=== Quickstart {example.Number:00}: {example.Title}");
    await example.RunAsync(llm, Console.Out);
    Console.WriteLine();
}

return 0;
