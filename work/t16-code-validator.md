---
pr:
pr_url:
jira:
epic: M4
status: todo
created: 2026-10-16
updated: 2026-10-16
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

- [ ] Forbid type declarations in cells
- [ ] Forbid blocking calls: `.Result`, `.Wait()`, `Thread.Sleep`, `Task.WaitAll`
- [ ] Require `await` on async calls (CS4014 as error)
- [ ] Forbid recursion into the current agentic method
- [ ] Forbid reflection / `System.Diagnostics.Process` / file APIs unless the agent opts in
- [ ] Forbid shadowing return-type names
- [ ] LLM-readable error messages
- [ ] Port the applicable validator tests

## Notes

Guardrails only (D12).
