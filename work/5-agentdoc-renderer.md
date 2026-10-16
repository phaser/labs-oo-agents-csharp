---
pr: 5
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/5
jira:
epic: M1
status: merged
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T05 — Agentdoc renderer: Doc()

## Context

Port `doc()`: render an agent type's fields, public methods, signatures and doc comments as prompt text.

Depends on: T03. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/agentdoc/ (except _pformat.py)`

## Progress

- [x] Read type metadata by reflection (`NullabilityInfoContext` for nullable)
- [x] Read doc comment text through `IAgentDocProvider` (T11 supplies the generated one; reflection fallback and a fake in tests)
- [x] Respect `[Hidden]` and private members; source order for methods
- [x] Render C# signatures (not Python) in the output
- [x] Port `Annotated` descriptions as `[Description]` (fields, properties, parameters, return value); `spec()` is not ported
- [x] Port tests from `tests/agentdoc/` that cover `doc()`; list skipped ones in the T06 task file

## Notes

Output format changes from Python to C# syntax. Record the format in porting-notes.

- Format and deviations: `docs/porting-notes.md`, section "`doc()` rendering".
- Public API: `AgentDocs.Doc`, `IAgentDocProvider`, `ReflectionDocProvider`, `AgentDocProviders` (registry, `Register` returns a disposable), `[Hidden]` in `Nooa`.
- Value printing: `ValuePreview` (internal) is the only place that prints values. T06 replaces it.
- No new NuGet package.
- Skipped upstream tests (pformat, truncation, Python-only features): see `work/t06-agentdoc-pformat.md`.
