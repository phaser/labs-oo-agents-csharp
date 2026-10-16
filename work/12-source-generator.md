---
pr: 12
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/12
jira:
epic: M3
status: open
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T11 — Source generator for agents

## Context

Emit bodies and metadata for `[Agentic]` partial methods (decisions D3–D6).

Depends on: T02. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/metaclass.py`
- `src/nooa/decorators.py`
- `src/nooa/ellipsis_detection.py`
- `src/nooa/strategies/current_call.py`

## Progress

- [x] Add `[Agentic]`, `[AgentOptions]`, `[Strategy]`, `[Hidden]`, `[NoSnapshot]`, `[NoTrace]` attributes
- [x] Emit each `[Agentic]` body: build `CurrentCall` (name, args, return type, prompt) and call the runtime
- [x] Embed class and method XML doc comments as prompt constants
- [x] Compile `{Member.Path}` placeholders to `Func<TAgent,string>` (D5); diagnostic for anything else
- [x] Emit per-type metadata for agentdoc (members in source order, doc text)
- [x] Diagnostics: class not partial, method not `Task`/`Task<T>`, `[Agentic]` on a generator or static method
- [x] Snapshot tests (plain string compare, not Verify: see Notes) and diagnostic tests
- [x] Remove `--ignore-exit-code 8` from `.github/workflows/ci.yml` (kept in T03 because `tests/Nooa.Generators.Tests` has no tests until this task)
- [x] Port the applicable cases of `test_metaclass.py`, `test_decorator_strategy.py`, `test_pure_python_method_rejection.py` (see Notes)

## Notes

Pre-ellipsis code is not supported (see porting-notes).

### Decisions (T11)

- **Return types:** `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`. The generated method is `async` and awaits the seam. A sync return is `NOOA0002`.
- **Diagnostics** (all errors): `NOOA0001` class (or a containing type) not `partial`; `NOOA0002` return type; `NOOA0003` static; `NOOA0004` generic method or generic class; `NOOA0005` ref/out/in/pointer/dynamic parameter; `NOOA0006` method not `partial` or already has a body; `NOOA0007` bad placeholder; `NOOA0008` class lacks `IAgenticTarget`; `NOOA0009` agent class is private or protected (the module initializer must reach it). A generator method (`IEnumerable<T>`, `IAsyncEnumerable<T>`) is `NOOA0002`. Stacked `[Strategy]` is a compiler error (`CS0579`, `AllowMultiple = false`).
- **Seam:** `Nooa.IAgenticTarget` (two `InvokeAgenticAsync` overloads), `Nooa.Strategies.CurrentCall`, `CallArgument`. Generated bodies call `((IAgenticTarget)this)`. T12 notes list what `CurrentCall` still lacks.
- **Class metadata:** `Nooa.AgentMetadata(AgentType, Llm, SystemPrompt)` in `AgentMetadataRegistry`. `[Strategy]` lands in `CurrentCall.StrategyType`. Resolution is T12.
- **Registration:** one `[ModuleInitializer]` class per assembly calls `Type.__NooaRegister()` of each agent class. It registers the doc provider and the metadata. Consumers must target net5.0 or later.
- **Doc provider:** nested class of the agent, so it can use private types. It matches members by `MetadataToken` (and `(token, position)` for parameters, `-1` is the return value). Text is `<summary>` + `<remarks>`, placeholders left as written.
- **Member order:** no seam change. Roslyn keeps the metadata order of a partial method at the declaration site, so `MetadataToken` order stays source order. A test proves it (`Doc_OfAgentType_ShowsGeneratedMethodsInSourceOrder`).
- **Placeholders:** `{A.B.C}` on `this` only. Each segment must be an accessible instance field or property (also private and inherited). Method parameters are not placeholders. `?.` is used after a reference-type segment, so a null link renders as empty. Values format with the invariant culture. Class doc comment placeholders work the same way (system prompt).
- **Doc comment reading:** from syntax trivia, with a hardened XML reader (no DTD, no resolver). It works with and without `GenerateDocumentationFile`. Invalid XML gives no doc text.
- **Cancellation token:** only a trailing `CancellationToken` parameter flows to the seam. Another position is an ordinary argument.
- **Verify not used.** `Verify.SourceGenerators` 2.5.0 (MIT, 2024-09-18) needs `Verify` 26.5 or newer, while `Verify.XunitV3` 33.3.4 (MIT, 2026-10-16, younger than 14 days) needs `xunit.v3.extensibility.core` 4.0.2 (we pin 4.0.1). The tree would also add several transitive packages to license-check. The tests compare the generated text with a raw string (`Snapshots.cs`) instead.
- **Tests:** generator tests run Roslyn in memory (`GeneratorHost`), including compile-and-run of the output. `tests/Nooa.Tests/Generated/` compiles a real agent with the generator as an analyzer and a hand-written `RecordingAgent` seam implementation.
- **Ported tests:** only declaration-rule cases apply: generator methods (`NOOA0002`), stacked `@strategy` (`CS0579`), `@strategy` on a non-LLM method, helper methods are not touched. Tracing, hooks, wrapping, `@no_trace` behaviour, method-body rejection at run time and strategy stacking at run time belong to T12 (wrapper, hooks) or do not apply. `test_pure_python_method_rejection.py` tests the REPL (T15/T17).
- **Packaging:** `Nooa.Tests` references the generator as an analyzer by project reference. The NuGet layout (analyzer inside the `Nooa` package) is not done here.
- `.github/workflows/ci.yml` no longer has `--ignore-exit-code 8`.

### From T05 (contract the generator must meet)

- `[Hidden]` already exists: `Nooa.HiddenAttribute` (`src/Nooa/HiddenAttribute.cs`, targets: field, property, method). Reuse it in the attribute checklist item. Widen `AttributeUsage` here if the generator needs more targets.
- Doc text seam: emit an implementation of `Nooa.Agentdoc.IAgentDocProvider` and register it with `AgentDocProviders.Register` (module initializer). `AgentDocs.Doc` asks registered providers first, then falls back to `[Description]`.
  - `GetSummary(MemberInfo)` returns the `<summary>` text (plus `<remarks>` if wanted) of a type, field, property or method. Return null when there is none. Multi-line text is allowed, `Doc` writes it as `///` lines.
  - `GetParameterSummary(ParameterInfo)` returns the `<param>` text. For the return parameter (`method.ReturnParameter`) it returns the `<returns>` text.
  - Match members by reflection identity (`MemberInfo` equality), not by name, so overloads work.
- Member order: `Doc` uses `MetadataToken` order. If the generator emits an explicit member order list, add it to the provider seam and change `MemberReader` to use it.

