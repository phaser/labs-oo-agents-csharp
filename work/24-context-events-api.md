---
pr: 24
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/24
jira:
epic: M5
status: merged
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T18 — ContextApi, EventsApi, dynamic context

## Context

Port the model-facing `self.Context` and `self.Events` APIs and re-evaluated context expressions.

Depends on: T17. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/runtime/context.py`
- `src/nooa/runtime/events.py`
- `src/nooa/context_blocks/`

## Progress

- [x] Port `ContextApi` (indexer, `SetDynamic`, `Disable`, `Enable`)
- [x] Port `EventsApi` (`Query`, `Get`, `Collapse`, `Keys`)
- [x] Evaluate `Context.expr` as a C# expression through the REPL engine
- [x] Port quickstart 05, 07 and 08 as offline acceptance tests
- [x] Port tests: `tests/runtime/` context and events API tests

## Notes

Details are in `docs/porting-notes.md` ("ContextApi, EventsApi and expression blocks (T18)").
- `Context.Expr(text)` is one C# expression, checked by `CellCodeValidator` at set time and at each evaluation (new engine per evaluation, about 45 ms). Never read from a snapshot or a config.
- `EventsApi` input is bounded (200 results, key, text, regex and summary lengths). `EventManager.Filter` regex is now `NonBacktracking`, a timeout is no match.
- Opt-in to the model: `public new ContextApi Context => base.Context;` in the agent class.
- Also fixed: `NooaJson.Options` had no `TypeInfoResolver` (order-dependent `ToolSchema` test failure).
- Not ported: `set_static`, `set_dynamic(value=)`, deprecation warnings.
- Security review fixes: symbol-based `ForbiddenSelfCalls`, re-entrancy guard, failed/abandoned expression cap, `[Hidden]` agent members refused in cells (see porting-notes).
