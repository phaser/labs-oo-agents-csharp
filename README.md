# labs-oo-agents-csharp

A C# / .NET port of [NVIDIA Object Oriented Agents (NOOA)](https://github.com/NVIDIA-NeMo/labs-OO-Agents).
In NOOA an agent is a class. Fields are state. Methods are capabilities. Doc comments are prompts. Types are contracts.
A method with no body (`partial`) is run by a model loop.

Status: v1 is complete. See [`docs/porting-notes.md`](docs/porting-notes.md) for what v1 covers and the backlog.

## Install

You need the .NET 10 SDK (see `global.json`).

```bash
git clone https://github.com/phaser/labs-oo-agents-csharp
cd labs-oo-agents-csharp
dotnet build Nooa.slnx -c Release
dotnet test --solution Nooa.slnx -c Release --filter "Category!=Live"
```

The tests run offline. They need no network and no API key.

To use the NuGet package, run `dotnet add package Nooa`. One package holds the library and the source generator (as an analyzer).
The package is an alpha (prerelease). Use `dotnet add package Nooa --prerelease`.
To test a local build, run `dotnet pack src/Nooa -c Release -o artifacts/packages` and add that folder as a package source.
`examples/PackageConsumer` shows this: a console app with one `[Agentic]` agent that restores `Nooa` from the local feed and runs offline.
Pass the packed version: `dotnet run --project examples/PackageConsumer -p:NooaVersion=<version>`.
The version is the name of the `.nupkg` file after `Nooa.`.

To work on this repo instead, reference `src/Nooa/Nooa.csproj` and the analyzer `src/Nooa.Generators/Nooa.Generators.csproj`
(`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`). `examples/Quickstart/Quickstart.csproj` shows the setup.

## Your first agent

```csharp
using Nooa;

/// <summary>You are an agent specializing in analyzing customer feedback.</summary>
public partial class FeedbackAgent(AgentOptions? options = null) : Agent(options)
{
    /// <summary>Analyze customer feedback for sentiment and key topics in one sentence.</summary>
    [Agentic]
    public partial Task<string> AnalyzeFeedbackAsync(string text);
}
```

The class comment is the system prompt. The method comment is the task. The generator writes the method body.
Pass a model client in the options, then call the method:

```csharp
var (_, llm) = Nooa.Util.Quickstart.CreateDefaultLlm();
var agent = new FeedbackAgent(new AgentOptions { Llm = llm });
Console.WriteLine(await agent.AnalyzeFeedbackAsync("Great product, but shipping was slow"));
```

## Quickstart examples

`examples/Quickstart` has the eight upstream quickstarts. Each one runs against a real model and costs money.
Set `NVIDIA_API_KEY`, `OPENAI_API_KEY` or `NVIDIA_INFERENCE_API_KEY`.
For another OpenAI-compatible endpoint, set `OPENAI_API_KEY` to its key, `OPENAI_BASE_URL` to its URL and `NOOA_MODEL` to its model name. The URL must be https, or http to a loopback host.
Then run:

```bash
dotnet run --project examples/Quickstart -- 1     # one example, 1 to 8
dotnet run --project examples/Quickstart -- all
```

| No. | Topic |
|-----|-------|
| 01 | First generation method |
| 02 | Structured output with a typed result |
| 03 | Methods as tools (CodeAct) |
| 04 | Predict and CodeAct strategies |
| 05 | Progressive disclosure with `Doc()` |
| 06 | Tracing |
| 07 | Dynamic prompts (`{Property}` in doc comments) |
| 08 | Context blocks |

`QuickstartAcceptanceTests` runs all eight offline with a scripted fake model. `QuickstartLiveTests` runs them
against a real model. Run only those with `dotnet test --solution Nooa.slnx -c Release --filter "Category=Live"`.

## Security

**The in-process REPL is not a security boundary.** The CodeAct strategy runs C# that the model writes
inside your process. That code has the rights of your process.

- The validator that checks each cell is a guardrail. It refuses known dangerous APIs. It does not stop a determined attacker.
- A cell time limit stops a cell that checks its cancel token. A tight loop that ignores the token cannot be stopped in the process.
- Run agents that handle untrusted input in a separate, restricted environment (container or virtual machine).
- A process sandbox for REPL cells is backlog item B01. Until B01 is done, treat model-written code as untrusted.

Credentials come from the environment or from `secrets.yaml` under the trust rules. Never commit a key.
Traces scrub known secret patterns, but you must still not put secrets in prompts.

## Release

The version comes from the git tag (MinVer). A commit before any tag gets `0.1.0-alpha.0.N`.

1. Make sure `main` is green.
2. Create a tag: `git tag v0.1.0-alpha.N`.
3. Push the tag: `git push origin v0.1.0-alpha.N`.
4. The `publish` workflow tests, packs and pushes to nuget.org. It uses nuget.org Trusted Publishing: a policy for `phaser/labs-oo-agents-csharp` and `publish.yml` on nuget.org, and the repository variable `NUGET_USER` (the nuget.org user name). No API key is stored.
5. Check the package on nuget.org.

A published version cannot be deleted. You can only unlist it. Do not reuse a tag.

## Credit and license

This project is a translation of [NVIDIA-NeMo/labs-OO-Agents](https://github.com/NVIDIA-NeMo/labs-OO-Agents)
by NVIDIA CORPORATION & AFFILIATES. Upstream is the specification for behavior, names and tests.

License: Apache License 2.0, the same as upstream. See [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).
The `NOTICE` file names upstream. Each ported file names its upstream source in its header.
