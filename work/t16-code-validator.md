---
pr:
pr_url:
jira:
epic: M4
status: open
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T16 — Code validator for REPL cells

## Context

Port the AST policy checks as Roslyn syntax and semantic walkers.

Depends on: T15. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/runtime/code_validator.py`
- `src/nooa/runtime/restrictions.py`
- `src/nooa/runtime/async_safety.py`

## Progress

- [x] Forbid type declarations in cells
- [x] Forbid blocking calls: `.Result`, `.Wait()`, `Thread.Sleep`, `Task.WaitAll`
- [x] Require `await` on async calls (CS4014 as error)
- [x] Forbid recursion into the current agentic method
- [x] Forbid reflection / `System.Diagnostics.Process` / file APIs unless the agent opts in
- [x] Forbid shadowing return-type names
- [x] LLM-readable error messages
- [x] Port the applicable validator tests

## Notes

Guardrails only (D12). Decisions and deviations: `docs/porting-notes.md` section "Code validator (T16)".

- Plugs into `ICellValidator`: `CellCodeValidator` (one per engine, keeps the accepted cells as a script chain).
- Opt-in groups (`RestrictedApiGroup`): FileSystem, Network, Process, Environment, Reflection, NativeCode. Blocking calls, unawaited tasks, type declarations, recursion and return-type shadowing have no opt-out.
- Not done: variables that the engine declares for method arguments are not visible to the validator (restricted type names are refused by text instead). The wiring of the validator into the strategy (`ForbiddenSelfCalls`, `ReturnType`) is for T17.
