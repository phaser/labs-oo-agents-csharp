# Port plan: NOOA (Python) → C#

Status: accepted direction, 2026-10-16. Tracker: [`work/index.md`](../work/index.md).

## 1. Upstream baseline

| Item | Value |
|------|-------|
| Repo | https://github.com/NVIDIA-NeMo/labs-OO-Agents |
| Pinned commit | [`1454b78`](https://github.com/NVIDIA-NeMo/labs-OO-Agents/commit/1454b78c8f0a85c97eab1c6a91cc8cfc95b34de5) (2026-10-16) |
| Size | ~320k lines of Python in 1,499 files. Core `src/nooa` is ~95k lines; `tests/` is ~160k lines. |
| License | Apache 2.0 |

Run `scripts/clone-reference.sh` to get the checkout. The script holds the pin.

Moving the pin is a deliberate task. It gets its own tracker item and a diff review of upstream changes.

### Upstream modules (core package `src/nooa`)

| Module | Lines | Purpose | v1 |
|--------|------:|---------|----|
| `runtime/` | 20.1k | `ActorRuntime`, method wrapper, REPL `execute_code`, code validator, event manager, context manager/builder, channels, sandbox | partial |
| `unifiedllm/` | 10.7k | LLM clients over litellm, tool schemas, retry, reasoning replay, cache boundaries, admission control, model registry, onboarding wizard | partial |
| `strategies/` | 9.2k | CodeAct, CodeActV2, Predict, Decide, Template, Reflexion, PurePython, prefill, loop guard | partial |
| `agentdoc/` | 6.9k | `doc()` / `pformat()`: renders objects and APIs as prompt text, with truncation | yes |
| `trace_explorer/`, `viewer/` | 11.8k | Trace query library; FastAPI + React viewer | no |
| `tracing/` | 4.6k | OpenTelemetry + OpenInference spans, OTLP/JSONL exporters | thin |
| `tools/` | 3.8k | `ShellTools` (persistent bash), `TodoManager`, `WebPublisher`, skill/method writing | shell, todo |
| `mcp/` | 2.7k | MCP client (stdio/SSE/HTTP), OAuth 2.1, dynamic tool classes | no |
| `context_blocks/` | 2.2k | Event IR, context blocks, renderer, formatters | yes |
| `atif/` | 2.2k | ATIF-v1.7 trajectory export | no |
| `storage/` | 2.1k | Event + snapshot persistence (in-memory, SQLite, JSON) | yes |
| `decisions/` | 1.7k | Typed Boolean / Choice / Score decisions | no |
| core files | ~7k | `agent.py`, `metaclass.py`, `decorators.py`, `ellipsis_detection.py`, `events.py`, `errors/`, `config/`, `llm_types.py`, `skill*.py`, `interactive.py`, `standalone.py`, `media.py`, layered config, secrets | mostly |

Workspace packages `nooa-cli`, `nooa-acp`, `nooa-memory`, `nooa-bench`, and `util/eval_pipeline`, are out of v1.

## 2. Decisions

| # | Decision | Choice | Reason |
|---|----------|--------|--------|
| D1 | Action language of the REPL | **C#**, via Roslyn scripting | Typed agent methods are callable directly; no cross-language bridge. Cristian chose this on 2026-10-16. |
| D2 | v1 scope | **Core + quickstart 01–08** | Cristian chose this on 2026-10-16. See §4. |
| D3 | Marking agentic methods | `[Agentic] public partial Task<T> M(...);` and an incremental source generator emits the body | C# has no `...` body. A partial method with no body is a compile error until the generator fills it in, so an unmarked stub cannot slip through. |
| D4 | Prompts | XML doc comments (`/// <summary>`) on the class and method; the generator embeds them as string constants | Closest analogue to docstrings. Doc comments are not in the assembly, so the generator must carry them. |
| D5 | `{expr}` templates in prompts | The generator compiles `{Field}` placeholders into a `Func<TAgent,string>` | Removes `eval` at runtime. Only member access paths are allowed. |
| D6 | Class-level config (`class X(Agent, llm=...)`) | `[AgentOptions]` attribute for static values; constructor `AgentOptions` for runtime values | `__init_subclass__` kwargs have no C# equivalent. |
| D7 | Typed I/O | `System.Text.Json` + `JsonSchemaExporter`; records with `required` members; `[Description]` for field docs | Replaces pydantic. Validation errors are fed back to the model, as upstream does. |
| D8 | LLM transport | `Microsoft.Extensions.AI` `IChatClient` as the transport. A NOOA-owned `LlmResponse` (ordered parts, opaque `JsonNode Native` per part) sits on top. | M.E.AI covers chat, tools, structured output, usage and middleware. Lossless reasoning replay and cache breakpoints need the own model. |
| D9 | Context propagation | `AsyncLocal<T>` | Replaces `contextvars`. |
| D10 | Generation lock | `SemaphoreSlim(1,1)` per agent + `AsyncLocal` "in generation session" flag for re-entry | Same as upstream `_generation_lock`. |
| D11 | Persisted values | JSON only. Polymorphic types only through an explicit `[JsonDerivedType]` allowlist. No `BinaryFormatter`, no Newtonsoft `TypeNameHandling`. | Deserialization safety. |
| D12 | REPL isolation in v1 | In-process Roslyn scripting with a restricted reference set, a syntax/semantic validator, and per-cell timeouts. **Not a security boundary.** | Same position as upstream's default `inprocess` backend. A process sandbox is backlog item B01. Docs and the README must say this clearly. |
| D13 | Object rendering (`pformat`, `repr`) | Reflection-based renderer in `Agentdoc` | C# has no `repr`. |
| D14 | Plugin discovery (entry points) | Not in v1. Skills are registered in code. | No v1 example needs discovery. |
| D15 | YAML config | YamlDotNet with the same layer order: bundled → `~/.config/nooa` → `.nooa/` → env var | Keeps upstream config files usable. |
| D16 | Python generators on agents | Out of v1 | Upstream wraps them for tracing only. |

### Target agent shape

```csharp
public record OrderResult(bool CanFulfill, double TotalCost, List<string> UnavailableItems);

/// <summary>You are an agent that checks inventory using deterministic helper methods.</summary>
[AgentOptions(Llm = "default")]
public partial class InventoryAgent : Agent
{
    private readonly Dictionary<string, (int Stock, double Price)> _inventory = new() { ... };

    /// <summary>Get current stock for an item.</summary>
    public int GetStock(string item) => _inventory.TryGetValue(item, out var v) ? v.Stock : 0;

    /// <summary>Check if order can be fulfilled within budget.</summary>
    [Agentic]
    public partial Task<OrderResult> CanFulfillOrder(List<string> items, double budget);
}
```

In a CodeAct cell the model writes C# against typed globals:

```csharp
var total = 0.0;
foreach (var i in items) total += self.GetPrice(i);
ReturnResult(new OrderResult(self.GetStock("orange") > 0, total, ["orange"]));
```

## 3. Solution layout

| Project | Target | Content |
|---------|--------|---------|
| `src/Nooa` | `net10.0` | Agent base, runtime, strategies, LLM layer, context blocks, agentdoc, storage, tracing, tools |
| `src/Nooa.Generators` | `netstandard2.0` | Incremental source generator and its diagnostics (Roslyn requirement) |
| `tests/Nooa.Tests` | `net10.0` | xUnit, mirrors `src/Nooa` |
| `tests/Nooa.Generators.Tests` | `net10.0` | Generator snapshot + diagnostic tests |
| `examples/Quickstart` | `net10.0` | Console app with quickstart 01–08 |

Namespaces follow folders: `Nooa.Runtime`, `Nooa.Strategies`, `Nooa.Llm`, `Nooa.ContextBlocks`, `Nooa.Agentdoc`, `Nooa.Storage`, `Nooa.Tracing`, `Nooa.Tools`, `Nooa.Config`, `Nooa.Errors`.

The REPL uses `Microsoft.CodeAnalysis.CSharp.Scripting`, which is a large dependency. Split it into `src/Nooa.CodeAct` only if trimming or startup time is measured as a problem (AGENTS.md: no speculative structure).

### Dependencies (v1)

| Package | Use |
|---------|-----|
| `Microsoft.Extensions.AI` (+ `.OpenAI`) | LLM transport |
| `Microsoft.CodeAnalysis.CSharp.Scripting` | REPL |
| `Microsoft.CodeAnalysis.CSharp` | Generator (build-time only) |
| `Microsoft.Data.Sqlite` | SQLite storage |
| `YamlDotNet` | Layered config, model registry |
| `OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` | Tracing export |
| `xunit.v3` (Microsoft.Testing.Platform), `Verify.SourceGenerators` | Tests |

Each dependency is license-checked when its task adds it.

## 4. v1 scope

**In:**
- Agent model, generator, `[Agentic]`, `[Hidden]`, `[NoSnapshot]`, `[Strategy]` override.
- `agentdoc` `Doc()` and `PFormat()` with truncation.
- Context blocks, event IR, renderer (XML + Markdown formatters), `ContextApi`, `EventsApi`, dynamic context.
- LLM layer: `LlmResponse`, `IChatClient` adapter (OpenAI-compatible endpoint first), tool schemas, structured output, retry, usage, model registry aliases, reasoning levels, `FakeLlmClient`.
- Runtime: `ActorRuntime`, method wrapper, event manager, in-memory + SQLite storage, hooks/middleware (minimal).
- Strategies: Template, Predict, CodeAct (with loop guard and prefill).
- REPL: Roslyn engine, validator, output capture, `Out[n]`, timeouts.
- Tracing: `ActivitySource` with OpenInference attribute names; OTLP and JSONL export.
- Tools: `ShellTools` (POSIX), `TodoManager`.
- Quickstart 01–08 as examples and as offline acceptance tests.

**Backlog (out of v1, in priority order):**

| Id | Item | Upstream |
|----|------|----------|
| B01 | Process sandbox for REPL cells (worker process, RPC proxy for `self`, OS limits) | `runtime/sandbox/` |
| B02 | MCP client (official ModelContextProtocol C# SDK) | `mcp/` |
| B03 | Decisions + `DecideStrategy` | `decisions/`, `strategies/decide.py` |
| B04 | Summarization agent + context-error archival | `agents/summarization.py` |
| B05 | ATIF export | `atif/` |
| B06 | Skills registry + `TextSkill` + slash commands | `skill*.py`, `slash_dispatch.py` |
| B07 | Channels, producers, `InteractiveAgent` | `runtime/channels.py`, `producers*.py`, `interactive.py` |
| B08 | Admission control | `unifiedllm/admission.py`, `controlled.py` |
| B09 | Cache boundaries + Anthropic / Responses adapters + lossless reasoning replay | `unifiedllm/` |
| B10 | CodeActV2, Reflexion, PurePython strategies | `strategies/` |
| B11 | Media (`Image`, `Audio`, …) and `show()` | `media.py`, `runtime/media_capture.py` |
| B12 | Memory package | `packages/nooa-memory` |
| B13 | CLI, ACP, bench, viewer, trace explorer | `packages/*`, `viewer/`, `trace_explorer/` |
| B14 | `PFormat` does not return on an endless lazy sequence (T17 guards the CodeAct path only) | `utils/pformat.py` |
| B15 | Live run of quickstart 02 and 04 (Predict) on an endpoint that supports `response_format` | `quickstart/02`, `quickstart/04` |

## 5. Milestones and tasks

| Milestone | Tasks | Exit check |
|-----------|-------|------------|
| M0 Bootstrap | T01 plan, T02 solution skeleton + CI | `dotnet build` / `test` / `format` green in CI |
| M1 Foundations | T03 errors + config, T04 event IR + `LlmResponse`, T05 agentdoc renderer, T06 agentdoc truncation, T07 storage | Ported upstream tests for these modules pass |
| M2 LLM layer | T08 `ILlmClient` + `FakeLlmClient`, T09 `IChatClient` adapter + tools + structured output + retry, T10 model registry + reasoning levels | Fake and live (opt-in) calls return typed `LlmResponse` |
| M3 Agent model | T11 source generator, T12 `Agent` + `ActorRuntime` + method wrapper + event/context managers, T13 context renderer, T14 Template + Predict | Quickstart 01, 02 pass offline |
| M4 CodeAct | T15 Roslyn REPL engine, T16 code validator, T17 CodeAct strategy + C# prompts | Quickstart 03, 04 pass offline |
| M5 Context and tracing | T18 `ContextApi` / `EventsApi` / dynamic context, T19 tracing | Quickstart 05–08 pass offline |
| M6 Tools | T20 `ShellTools`, T21 `TodoManager` | Ported tool tests pass on macOS + Linux |
| M7 Acceptance | T22 quickstart examples + acceptance suite + live smoke run, README | All 8 quickstarts pass offline; live smoke run recorded |

Dependency graph:

```
T02 ─┬─ T03 ─┬─ T04 ─┬─ T07
     │       │       └─ T08 ─ T09 ─ T10
     │       └─ T05 ─ T06
     └─ T11
T04,T05,T08,T11 ─ T12 ─ T13 ─ T14 ─ T15 ─ T16 ─ T17 ─┬─ T18
                                                     ├─ T19
                                                     └─ T20, T21
T14..T21 ─ T22
```

## 6. Test strategy

- **Port tests with code.** Each task translates the upstream tests for its modules. A test that cannot apply to C# is listed in `docs/porting-notes.md` with the reason.
- **Offline by default.** `FakeLlmClient` gives scripted responses. `dotnet test` needs no network and no key.
- **Live tests are opt-in.** `[Trait("Category", "Live")]`; endpoint and key come from environment variables.
- **Acceptance.** Each quickstart has a test that runs it against recorded fake responses and checks the typed result.
- **Generator.** Snapshot tests for emitted code; diagnostic tests for misuse (non-partial class, sync agentic method, generator method).

## 7. Risks

| Risk | Effect | Response |
|------|--------|----------|
| Model-written C# runs in-process with full CLR access | Host compromise if the model or a prompt injection writes hostile code | D12: say "not a boundary" in docs; restricted references + validator as guardrails; B01 is the first backlog item. |
| Roslyn script assemblies are not collectible | Memory grows per cell in long sessions | Measure in T15. Mitigate with a session cell cap or a worker process (B01). |
| No hard kill for a runaway cell in-process | A tight loop blocks a thread past the timeout | Cooperative cancellation in v1; hard kill needs B01. Document it. |
| Prompts and error hints are written for Python | Model quality drops on C# | T17 rewrites every prompt and hint for C#; acceptance tests cover each quickstart. |
| Models write C# less reliably than Python in a REPL | More retries, higher cost | Measure retry counts in the T22 live smoke run. |
| `agentdoc` is large (~7k lines) and Python-repr based | Schedule risk | Split into T05 (renderer) and T06 (truncation). |
| Upstream moves fast | Drift | Fixed pin; a pin move is its own task. |
